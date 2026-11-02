# SQL Client TLS / SSPI Diagnostics

**Run date:** 30 Oct 2026. **Mode:** read-only local diagnostics. No connection string values or credentials are recorded here. No registry, certificate, machine security, SQL Server configuration or database was changed.

## Verified findings

| Check | Finding |
|---|---|
| Interactive execution identity | `AL414\\AL414`; authenticated with Windows `NTLM`, impersonation level `None`, not LocalSystem. This is the diagnostic process context, not the SQL Server service identity. |
| SQL Server service | `MSSQLSERVER` is running, configured for automatic delayed start, service account `NT Service\\MSSQLSERVER`. `SQLBrowser` is disabled and runs as LocalService. CIM service inventory access was denied; `sc.exe` read-only queries provided the service facts. |
| Instance/protocol settings | Registry reports instance `MSSQL17.MSSQLSERVER`; TCP/IP and Named Pipes disabled; Shared Memory enabled. TCP port is configured as 1433, but listener inspection found no listening socket on 1433/1434. Thus remote TCP connectivity is not available on the inspected instance. |
| Server encryption configuration | `ForceEncryption=0`; no strict-encryption value was configured; no explicit certificate thumbprint was associated in the inspected SQL instance registry values. This does not demonstrate that TLS is disabled for clients requesting encryption, nor does it establish which fallback certificate/chain SQL Server presents. |
| Installed client providers | ODBC Driver 17 and 18 are registered. Reported file versions: Driver 17 `2017.1710.0006.01`; Driver 18 `2018.0186.0002.01`. The discovered `sqlcmd` executable is under the ODBC 17 tools directory, so its observed failure is evidence for Driver 17 only. API assets reference Microsoft.Data.SqlClient 6.1.7. |
| .NET SqlClient diagnostic | A loopback diagnostic with `Encrypt=True` and `TrustServerCertificate=False` failed before query/login with SqlException 20: “The instance of SQL Server you attempted to connect to requires encryption but this machine does not support it.” The exact transport selected was not captured; TCP is disabled, so this is not evidence of a successful TCP/TLS negotiation with the SQL engine. |
| ODBC/SSPI diagnostic | ODBC 17 `sqlcmd` reported “Encryption not supported on the client” and “No credentials are available in the security package,” then failed before SQL authentication. This directly establishes a client-side encryption/SSPI failure for that executable/provider path, not a SQL login rejection. |
| Schannel evidence | No matching Schannel System log events were found in the queried 14-day window. SQL ERRORLOG path inspection was denied, so server-side TLS errors could not be reviewed. |
| Separate isolated instance | None was identified and validated in this session. LocalDB inventory access was denied. No properly encrypted connection to a separate isolated instance was established. |

## Assessment

The immediate observed failure occurs in the client encryption/Windows security provider path before SQL authentication. The ODBC 17 message specifically indicates that this provider path could not supply the needed encryption/SSPI support. The .NET error also identifies client encryption support. SQL authentication and database authorization therefore remain untested.

Separately, the inspected default instance has TCP disabled and no TCP listener, so it is not a usable isolated SQL rehearsal target over TCP in the current configuration. Certificate trust is **unverified**: no explicit certificate association was found, but certificate presentation, chain, name matching, Schannel policy and actual negotiated protocol were not observed. Do not conclude that a certificate is invalid based only on the client error.

## Safe next diagnostic steps

The repository owner/DBA should provide a separately isolated test instance with TCP enabled only through its approved change process, a server certificate whose name matches the test DNS name and chains to a trusted issuer on the test client, and a test identity authorized only to that test database. Retest using strict encryption and certificate validation; capture the negotiated provider, protocol/cipher, SQL error log and client Schannel/SSPI events. Keep `TrustServerCertificate=False`. Do not weaken production encryption or change machine-wide policy to make the current workstation test pass.

## Blocker

Migration rehearsal, persisted tenant tests, backup/restore verification and SQL-backed workflow compatibility remain blocked because no isolated, properly encrypted SQL connection was established. No production database was accessed. Synthetic EF InMemory results must not be described as persisted database isolation evidence.
