BEGIN TRANSACTION;
GO

ALTER TABLE [Projects] ADD [OurCompanyId] uniqueidentifier NULL;
GO

CREATE TABLE [CompanyMasters] (
    [Id] uniqueidentifier NOT NULL,
    [CompanyName] nvarchar(300) NOT NULL,
    [DisplayName] nvarchar(300) NOT NULL,
    [Address] nvarchar(2000) NULL,
    [Telephone] nvarchar(150) NULL,
    [Fax] nvarchar(150) NULL,
    [Website] nvarchar(300) NULL,
    [Email] nvarchar(500) NULL,
    [LogoPath] nvarchar(255) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_CompanyMasters] PRIMARY KEY ([Id])
);
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Address', N'CompanyName', N'CreatedAt', N'DisplayName', N'Email', N'Fax', N'IsActive', N'LogoPath', N'Telephone', N'UpdatedAt', N'Website') AND [object_id] = OBJECT_ID(N'[CompanyMasters]'))
    SET IDENTITY_INSERT [CompanyMasters] ON;
INSERT INTO [CompanyMasters] ([Id], [Address], [CompanyName], [CreatedAt], [DisplayName], [Email], [Fax], [IsActive], [LogoPath], [Telephone], [UpdatedAt], [Website])
VALUES ('235b2bcf-183f-5be0-86ab-0cdb29195920', N'P.O Box: 24496, 20th Floor, Tower 400  Mina Road, Sharjah, UAE', N'ARIES MARINE & ENGG. SERVICES', '2026-10-07T00:00:00.0000000+00:00', N'Aries Marine & ENGG .SERVICES - DF', N'design@ariesgroup.ae, offshoredesign@ariesgroup.ae', N'+971 (6) 550 3100', CAST(1 AS bit), N'aries_mar_engg.png', N'+971 (6) 550 3300', '2026-10-07T00:00:00.0000000+00:00', N'www.ariesmar.com'),
('31bdf1cc-fb23-547f-9b85-fbd048a24705', N'PO. Box: 15368, 3rd Floor, 303-B, Jaidah Square Umm Ghuwailina, 63 Airport Road - Zone 27, Doha, Qatar', N'ARIES MARINE CO. W.L.L', '2026-10-07T00:00:00.0000000+00:00', N'Aries Marine CO. WLL - QD', N'designqa@ariesgroup.ae', NULL, CAST(1 AS bit), N'aries_mar_engg.png', NULL, '2026-10-07T00:00:00.0000000+00:00', N'www.ariesmar.com'),
('50875e90-8c8e-57c5-8903-3810fb65ffba', N'P.O Box: 3965, Warehouse 40 Mussafah Ind.Area, Abu Dhabi, UAE', N'ARIES OIL FIELD SERVICES L.L.C', '2026-10-07T00:00:00.0000000+00:00', N'Aries Oil Field Services L L .C - DU', N'designauh@ariesofs.ae', N'+971 (2) 550 8996', CAST(1 AS bit), N'aries_mar_engg.png', N'+971 (2) 550 8893', '2026-10-07T00:00:00.0000000+00:00', N'www.ariesmar.com'),
('54601c1b-c9fd-5199-a72e-a169a05970ae', N'P.O Box: 24496, 20th Floor, Tower 400  Mina Road, Sharjah, UAE', N'ARIES MARINE & ENGG. SERVICES', '2026-10-07T00:00:00.0000000+00:00', N'Aries Marine & ENGG .SERVICES - DI', N'ideas@ariesofs.ae', N'+971 (6) 550 3100', CAST(1 AS bit), N'aries_mar_engg.png', N'+971 (6) 550 3300', '2026-10-07T00:00:00.0000000+00:00', N'www.ariesmar.com'),
('651862ed-98e7-5a95-9c6f-dce2dbacd941', N'P.O Box: 94354, AI Qayadah Bldg. Abu Hail, Dubai, UAE', N'ARIES MARINE L.L.C', '2026-10-07T00:00:00.0000000+00:00', N'Aries Marine L L .C - DS', N'ariesmarine@ariesgroup.ae', N'+971 (4) 299 7940', CAST(1 AS bit), N'aries_mar_engg.png', N'+971 (4) 299 7939', '2026-10-07T00:00:00.0000000+00:00', N'www.ariesmar.com'),
('9003d8ae-58d2-577d-a9b3-d145e9bb738f', N'P.O Box: 94354, Al Qayadah Bldg. Abu Hail, Dubai, UAE', N'ARIES MARINE L.L.C', '2026-10-07T00:00:00.0000000+00:00', N'Aries Marine L L .C - DM', N'dams@ariesgroup.ae', N'+971 (4) 299 7940', CAST(1 AS bit), N'aries_mar_engg.png', N'+971 (4) 299 7939', '2026-10-07T00:00:00.0000000+00:00', N'www.ariesmar.com'),
('986c73ab-be16-508b-a484-e2260d267ee5', N'Business Bay, Office #601, Buiding No. 1260, Road No.  2421, Block No.  324, P.O. Box. 11614, Juffair, Manama, Kingdom of Bahrain', N'ARIES MARINE WLL', '2026-10-07T00:00:00.0000000+00:00', N'Aries Marine WLL - DB', N'ariesbahrain@ariesgroup.ae', NULL, CAST(1 AS bit), N'aries_mar_engg.png', NULL, '2026-10-07T00:00:00.0000000+00:00', N'www.ariesmar.com'),
('9caaffe9-ed24-587e-91ea-764cb5876674', N'#22-03A, International Plaza 10 Anson Road, Singapore 079903', N'ARIES NETWORK PTE. LTD.', '2026-10-07T00:00:00.0000000+00:00', N'Aries Network PTE .LTD - DG', N'designsg@ariesgroup.ae', N'+65 6220 9058', CAST(1 AS bit), N'aries_mar_engg.png', N'+65 6220 9056', '2026-10-07T00:00:00.0000000+00:00', N'www.ariesmar.com'),
('cc0f18d3-8516-5c7b-99ad-54148151b25f', N'P.O Box: 24496, 20th Floor, Tower 400  Mina Road, Sharjah, UAE', N'ARIES MARINE & ENGG. SERVICES', '2026-10-07T00:00:00.0000000+00:00', N'Aries Marine & ENGG .SERVICES - DE', N'eit@ariesgroup.ae', N'+971 (6) 550 3100', CAST(1 AS bit), N'aries_mar_engg.png', N'+971 (6) 550 3300', '2026-10-07T00:00:00.0000000+00:00', N'www.ariesmar.com'),
('d31b47a1-91a5-5acf-b7bf-98610b0fcc2a', N'P.O Box: 24496, 20th Floor, Tower 400  Mina Road, Sharjah, UAE', N'ARIES MARINE & ENGG. SERVICES', '2026-10-07T00:00:00.0000000+00:00', N'Aries Marine & ENGG .SERVICES - DN', N'design@ariesgroup.ae', N'+971 (6) 550 3100', CAST(1 AS bit), N'aries_mar_engg.png', N'+971 (6) 550 3300', '2026-10-07T00:00:00.0000000+00:00', N'www.ariesmar.com'),
('fb2c0295-cc98-5af3-90e8-c78d8aee925d', N'Juffali Building (Fluor Arabia Tower) Office #504, King Fahd Road, Al Khobar 34423 Kingdom of Saudi Arabia', N'ARIES MARINE SERVICES CO. LTD.', '2026-10-07T00:00:00.0000000+00:00', N'Aries Marine Services CO .LTD - KD', N'ariesksa@ariesgroup.ae', NULL, CAST(1 AS bit), N'aries_mar_engg.png', NULL, '2026-10-07T00:00:00.0000000+00:00', N'www.ariesmar.com');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Address', N'CompanyName', N'CreatedAt', N'DisplayName', N'Email', N'Fax', N'IsActive', N'LogoPath', N'Telephone', N'UpdatedAt', N'Website') AND [object_id] = OBJECT_ID(N'[CompanyMasters]'))
    SET IDENTITY_INSERT [CompanyMasters] OFF;
GO

CREATE INDEX [IX_Projects_OurCompanyId] ON [Projects] ([OurCompanyId]);
GO

CREATE UNIQUE INDEX [IX_CompanyMasters_DisplayName] ON [CompanyMasters] ([DisplayName]);
GO

ALTER TABLE [Projects] ADD CONSTRAINT [FK_Projects_CompanyMasters_OurCompanyId] FOREIGN KEY ([OurCompanyId]) REFERENCES [CompanyMasters] ([Id]) ON DELETE NO ACTION;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261007131230_OurCompanyMaster', N'8.0.0');
GO

COMMIT;
GO

