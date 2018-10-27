namespace QBDiscordAssistant.Tournament
{
    public class Game
    {
        // TODO: Reader is in here and in Room. Try to remove the duplication.
        public Reader Reader { get; set; }

        public Room Room { get; set; }

        public Team[] Teams { get; set; }
    }
}
