namespace SonTinhThuyTinh.Combat
{
    // What an arena event (SeaBossArenaEvent) needs from a boss that fights inside a map: its health, a way to freeze it and a way to
    // put it back to a fresh fight after the player was knocked out.
    public interface IArenaBoss
    {
        Health Health { get; }
        void SetBattleEnded();
        void ResetForRetry();
    }
}
