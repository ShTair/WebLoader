namespace WebLoader
{
    class TargetParam
    {
        public string Protocol { get; set; }

        public string Host { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }

        public string EncodingName { get; set; }

        public string BasePath { get; set; }

        public string[] IgnorePaths { get; set; }

        public string[] UndeletableNames { get; set; }

        public string VaultPath { get; set; }
    }
}
