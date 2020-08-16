namespace WeatherLib.Dwd.OpenData
{
    public class WarningCell
    {
        public WarningCell(string id, string name, string shortName)
        {
            Id = id;
            Name = name;
            ShortName = shortName;
        }

        public string Id { get; }
        public string Name { get; }
        public string ShortName { get; }
        public bool IsCommune => Id.Length > 0 && Id[0] >= '5' && Id[0] <= '8';

        public override string ToString()
        {
            return $"{Id} - {Name} ({ShortName})";
        }
    }
}