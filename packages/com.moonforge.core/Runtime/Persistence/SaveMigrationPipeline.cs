using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Moonforge.Core.Persistence
{

    public sealed class SaveMigrationPipeline
    {
        private readonly Dictionary<int, ISaveMigration> _byFromVersion;

        public SaveMigrationPipeline(IEnumerable<ISaveMigration> migrations)
        {
            _byFromVersion = migrations.ToDictionary(x => x.FromVersion);
        }

        public string ApplyToLatest(string payload, int targetSchemaVersion)
        {
            string current = payload;
            int version = ReadSchemaVersion(current);
            while (version < targetSchemaVersion)
            {
                if (!_byFromVersion.TryGetValue(version, out ISaveMigration migration))
                {
                    break;
                }

                current = migration.Migrate(current);
                int next = ReadSchemaVersion(current);
                if (next <= version)
                {
                    break;
                }

                version = next;
            }

            return current;
        }

        private static int ReadSchemaVersion(string payload)
        {
            JObject root = JObject.Parse(payload);
            JToken? token = root["schemaVersion"];
            if (token is { Type: JTokenType.Integer })
            {
                return token.Value<int>();
            }

            return 0;
        }
    }
}
