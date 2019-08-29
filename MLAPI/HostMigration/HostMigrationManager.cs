using System.Collections.Generic;

namespace MLAPI.HostMigration
{
    public static class HostMigrationManager
    {
        // TODO: Add token to verify they dont claim others stuff
        public static readonly List<MigratableClient> MigratableClients = new List<MigratableClient>();
    }

    public struct MigratableClient
    {
        public ulong ClientId;
        public ulong MigrationKey;
    }
}
