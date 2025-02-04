////////////////////////////////////////////////////////////////////////////
//
// Copyright 2016 Realm Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
////////////////////////////////////////////////////////////////////////////

using System.Threading;
using System.Threading.Tasks;
using Realms.Helpers;
using Realms.Native;
using Realms.Schema;

namespace Realms
{
    /// <summary>
    /// Realm configuration specifying settings that affect the Realm's behavior.
    /// <br/>
    /// Its main role is generating a canonical path from whatever absolute, relative subdirectory, or just filename the user supplies.
    /// </summary>
    public class RealmConfiguration : RealmConfigurationBase
    {
        /// <summary>
        /// In order to handle manual migrations, you need to supply a migration callback to your
        /// <see cref="RealmConfiguration"/>. It will be called with a <see cref="Migration"/> instance containing
        /// the pre- and the post-migration <see cref="Realm"/>. You should make sure that the <see cref="Migration.NewRealm"/>
        /// property on it contains a database that is up to date when returning. The <c>oldSchemaVersion</c>
        /// parameter will tell you which <see cref="RealmConfigurationBase.SchemaVersion"/> the user is migrating
        /// <b>from</b>. They should always be migrating to the current <see cref="RealmConfigurationBase.SchemaVersion"/>.
        /// </summary>
        /// <param name="migration">
        /// The <see cref="Migration"/> instance, containing information about the old and the new <see cref="Realm"/>.
        /// </param>
        /// <param name="oldSchemaVersion">
        /// An unsigned long value indicating the <see cref="RealmConfigurationBase.SchemaVersion"/> of the old
        /// <see cref="Realm"/>.
        /// </param>
        public delegate void MigrationCallbackDelegate(Migration migration, ulong oldSchemaVersion);

        /// <summary>
        /// A callback, invoked when opening a Realm for the first time during the life
        /// of a process to determine if it should be compacted before being returned
        /// to the user.
        /// </summary>
        /// <param name="totalBytes">Total file size (data + free space).</param>
        /// <param name="bytesUsed">Total data size.</param>
        /// <returns><c>true</c> to indicate that an attempt to compact the file should be made.</returns>
        /// <remarks>The compaction will be skipped if another process is accessing it.</remarks>
        public delegate bool ShouldCompactDelegate(ulong totalBytes, ulong bytesUsed);

        /// <summary>
        /// Gets or sets a value indicating whether the database will be deleted if the <see cref="RealmSchema"/>
        /// mismatches the one in the code. Use this when debugging and developing your app but never release it with
        /// this flag set to <c>true</c>.
        /// </summary>
        /// <value><c>true</c> to delete the database on schema mismatch; <c>false</c> otherwise.</value>
        public bool ShouldDeleteIfMigrationNeeded { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a <see cref="Realm"/> is opened as readonly. This allows opening it
        /// from locked locations such as resources, bundled with an application.
        /// </summary>
        /// <value><c>true</c> if the <see cref="Realm"/> will be opened as readonly; <c>false</c> otherwise.</value>
        public bool IsReadOnly { get; set; }

        /// <summary>
        /// Gets or sets the migration callback.
        /// </summary>
        /// <value>
        /// The <see cref="MigrationCallbackDelegate"/> that will be invoked if the <see cref="Realm"/> needs
        /// to be migrated.
        /// </value>
        public MigrationCallbackDelegate? MigrationCallback { get; set; }

        /// <summary>
        /// Gets or sets the compact on launch callback.
        /// </summary>
        /// <value>
        /// The <see cref="ShouldCompactDelegate"/> that will be invoked when opening a Realm for the first time
        /// to determine if it should be compacted before being returned to the user.
        /// </value>
        public ShouldCompactDelegate? ShouldCompactOnLaunch { get; set; }

        /// <summary>
        /// Gets or sets the key, used to encrypt the entire Realm. Once set, must be specified each time the file is used.
        /// </summary>
        /// <value>Full 64byte (512bit) key for AES-256 encryption.</value>
        public new byte[]? EncryptionKey
        {
            get => base.EncryptionKey;
            set => base.EncryptionKey = value;
        }

        private static RealmConfigurationBase? _defaultConfiguration;

        /// <summary>
        /// Gets or sets the <see cref="RealmConfigurationBase"/> that is used when creating a new <see cref="Realm"/> without specifying a configuration.
        /// </summary>
        /// <value>The default configuration.</value>
        public static RealmConfigurationBase DefaultConfiguration
        {
            get => _defaultConfiguration ??= new RealmConfiguration();

            set
            {
                Argument.NotNull(value, nameof(value));
                _defaultConfiguration = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RealmConfiguration"/> class.
        /// </summary>
        /// <param name="optionalPath">Path to the realm, must be a valid full path for the current platform, relative subdirectory, or just filename.</param>
        public RealmConfiguration(string? optionalPath = null) : base(optionalPath)
        {
        }

        /// <summary>
        /// Clone method allowing you to override or customize the current path.
        /// </summary>
        /// <returns>An object with a fully-specified, canonical path.</returns>
        /// <param name="newConfigPath">Path to the realm, must be a valid full path for the current platform, relative subdirectory, or just filename.</param>
        public RealmConfiguration ConfigWithPath(string newConfigPath)
        {
            var ret = (RealmConfiguration)MemberwiseClone();
            ret.DatabasePath = GetPathToRealm(newConfigPath);
            return ret;
        }

        internal override SharedRealmHandle CreateHandle(RealmSchema schema)
            => SharedRealmHandle.Open(CreateNativeConfiguration(), schema, EncryptionKey);

        internal override Configuration CreateNativeConfiguration()
        {
            var result = base.CreateNativeConfiguration();

            result.delete_if_migration_needed = ShouldDeleteIfMigrationNeeded;
            result.read_only = IsReadOnly;
            result.invoke_migration_callback = MigrationCallback != null;
            result.invoke_should_compact_callback = ShouldCompactOnLaunch != null;

            return result;
        }

        internal override Task<SharedRealmHandle> CreateHandleAsync(RealmSchema schema, CancellationToken cancellationToken)
        {
            // Can't use async/await due to mono inliner bugs
            // If we are on UI thread will be set but often also set on long-lived workers to use Post back to UI thread.
            if (AsyncHelper.TryGetScheduler(out var scheduler))
            {
                return Task.Run(() =>
                {
                    using (CreateHandle(schema))
                    {
                    }
                }, cancellationToken).ContinueWith(_ => CreateHandle(schema), scheduler);
            }

            return Task.FromResult(CreateHandle(schema));
        }
    }
}
