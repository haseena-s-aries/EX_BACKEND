using Ex_SurveyApp_api.Models;

namespace Ex_SurveyApp_api.Controllers;

public sealed partial class EquipmentIdentificationController
{
    private static readonly IReadOnlyDictionary<string, string> RegisterFields = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["manufacturer"] = nameof(AreaEquipment.Manufacturer),
        ["model"] = nameof(AreaEquipment.Model),
        ["serialNumber"] = nameof(AreaEquipment.SerialNumber),
        ["hazardousAreaZone"] = nameof(AreaEquipment.HazardousAreaZone),
        ["exProtectionType"] = nameof(AreaEquipment.ExProtectionType),
        ["gasGroup"] = nameof(AreaEquipment.GasGroup),
        ["temperatureClass"] = nameof(AreaEquipment.TemperatureClass),
        ["ambientTemperatureRange"] = nameof(AreaEquipment.AmbientTemperatureRange),
        ["ipRating"] = nameof(AreaEquipment.IpRating),
        ["notifiedBody"] = nameof(AreaEquipment.NotifiedBody),
        ["certificateNumber"] = nameof(AreaEquipment.CertificateNumber),
        ["specialConditionsOfUse"] = nameof(AreaEquipment.SpecialConditionsOfUse)
    };

    private Dictionary<string, string> ApplyRegisterMetadata(AreaEquipment equipment, IReadOnlyDictionary<string, string?>? fields, string? description)
    {
        var errors = new Dictionary<string, string>();
        if (fields is null) return errors;
        var notApplicable = System.Text.Json.JsonSerializer.Deserialize<string[]>(equipment.NotApplicableFieldsJson ?? "[]") ?? [];
        if (Clean(description) is not null && notApplicable.Contains("description", StringComparer.OrdinalIgnoreCase))
            errors["description"] = "This field is marked Not Applicable.";
        var entry = db.Entry(equipment);
        foreach (var (key, raw) in fields)
        {
            if (!RegisterFields.TryGetValue(key, out var name))
            {
                errors[key] = "This field is not editable in the register.";
                continue;
            }

            var value = Clean(raw);
            var max = entry.Property(name).Metadata.GetMaxLength();
            if (value?.Length > max) errors[key] = $"Use at most {max} characters.";
            else if (value is not null && notApplicable.Contains(key, StringComparer.OrdinalIgnoreCase))
                errors[key] = "This field is marked Not Applicable.";
        }
        if (errors.Count == 0)
            foreach (var (key, value) in fields)
                entry.Property(RegisterFields[key]).CurrentValue = Clean(value);
        return errors;
    }

    private bool RegisterMetadataMatches(AreaEquipment equipment, IReadOnlyDictionary<string, string?>? fields) =>
        fields is null || fields.All(field => RegisterFields.TryGetValue(field.Key, out var name) &&
            Equals(db.Entry(equipment).Property(name).CurrentValue, Clean(field.Value)));
}
