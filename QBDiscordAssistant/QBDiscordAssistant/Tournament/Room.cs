namespace QBDiscordAssistant.Tournament
{
    public class Room
    {
        public string Name { get; set; }

        public Reader Reader { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is Room otherRoom)
            {
                return this.Name == otherRoom.Name;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return this.Name.GetHashCode();
        }
    }
}
