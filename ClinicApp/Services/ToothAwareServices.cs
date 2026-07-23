namespace ClinicApp.Services
{
    public static class ToothAwareServices
    {
        // Services that require tooth number input
        public static readonly HashSet<string> RequiresTeethInput =
            new(StringComparer.OrdinalIgnoreCase)
        {
            "Tooth Extraction",
            "Composite Filling",
            "Glass Ionomer Filling",
            "Temporary Filling",
            "Dental Crown Placement (Porcelain)",
            "Dental Crown Placement (Plastic)",
            "Bridge Placement",
            "Pit & Fissure Sealants",
            "Pediatric Filling",
            "Space Maintainer"
        };

        // Condition to apply on the dental chart per service
        public static string GetCondition(string serviceName) =>
            serviceName.ToLower() switch
            {
                var s when s.Contains("extraction") => "Extracted",
                var s when s.Contains("composite") => "Filled (Composite)",
                var s when s.Contains("glass ionomer") => "Filled (GI)",
                var s when s.Contains("temporary") => "Temporary Filling",
                var s when s.Contains("crown") => "Crown",
                var s when s.Contains("bridge") => "Bridge",
                var s when s.Contains("sealant") => "Sealant",
                var s when s.Contains("space") => "Space Maintainer",
                _ => "Treated"
            };

        public static bool NeedsTeethInput(string serviceName) =>
            RequiresTeethInput.Contains(serviceName);

        // Services that support installment payments
        public static readonly HashSet<string> InstallmentEligible =
            new(StringComparer.OrdinalIgnoreCase)
        {
            "Braces Installation",
            "Complete Denture (Ordinary)",
            "Complete Denture (Premium)",
            "Dental Crown Placement (Porcelain)",
            "Bridge Placement",
            "Partial Denture",
            "Retainer Delivery"
        };

        public static bool IsInstallmentEligible(string serviceName) =>
            InstallmentEligible.Contains(serviceName);
    }
}