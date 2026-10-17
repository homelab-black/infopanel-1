using DiskInfoToolkit.Interop;
using InfoPanel.Monitors;
using Microsoft.IdentityModel.Abstractions;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfoPanel.Models
{
    public class ScreenRenderManager
    {
        private static readonly ILogger Logger = Log.ForContext<PluginHostConnection>();

        private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, long>>
            _itemProfiles = new();

        private readonly ConcurrentDictionary<Guid, long> _screenVersion = new();

        public void AddItemToProfile(string itemKey, Guid profileId)
        {
            _screenVersion.TryAdd(profileId, 0);

            var profiles = _itemProfiles.GetOrAdd(itemKey, _ => new ConcurrentDictionary<Guid, long>());
            var newVersion = _screenVersion.AddOrUpdate(profileId, 1, (_, current) => current + 1);

            profiles.AddOrUpdate(profileId, newVersion, (_, _) => newVersion);
//            Logger.Debug("Added item: {ItemKey} to profile: {ProfileId}. Incremented version to: {Version}", itemKey, profileId, newVersion);
        }

        public void RemoveItemFromProfile(string itemKey, Guid profileId)
        {
            if (_itemProfiles.TryGetValue(itemKey, out var profiles))
            {
                profiles.TryRemove(profileId, out _);

                if (profiles.IsEmpty)
                {
                    _itemProfiles.TryRemove(itemKey, out _);
                }

                var newVersion = _screenVersion.AddOrUpdate(profileId, 1, (_, current) => current + 1);
//                Logger.Debug("Removed item: {ItemKey} from profile: {ProfileId}. Incremented version to: {Version}", itemKey, profileId, newVersion);
            }
        }

        public void IncVersionByItem(string itemKey)
        {
            if (!_itemProfiles.TryGetValue(itemKey, out var profiles))
                return;

            var profileIds = profiles.Keys.ToArray();

            foreach (var profileId in profileIds)
            {
                var newVersion = _screenVersion.AddOrUpdate(profileId, 1, (_, current) => current + 1);

                profiles.AddOrUpdate(profileId, newVersion, (_, _) => newVersion);
//                Logger.Debug("Incremented version for profile: {ProfileId} due to item: {ItemKey}. New version: {Version}", profileId, itemKey, newVersion);
            }
        }

        public long? GetItemKeyVersion(string itemKey, Guid profileId)
        {
            if (_itemProfiles.TryGetValue(itemKey, out var profiles))
            {
                if (profiles.TryGetValue(profileId, out var version))
                {
                    return version;
                }
            }
            return null;
        }

        public long GetVersion(Guid profileId) =>
            _screenVersion.GetValueOrDefault(profileId);

        public void RefreshAllVersions()
        {
            foreach (var profileId in _screenVersion.Keys)
            {
                var newVersion = _screenVersion.AddOrUpdate(profileId, 1, (_, current) => current + 1);

                foreach (var kvp in _itemProfiles)
                {
                    var itemKey = kvp.Key;
                    var itemProfiles = kvp.Value;

                    if (itemProfiles.ContainsKey(profileId))
                    {
                        itemProfiles.AddOrUpdate(profileId, newVersion, (_, _) => newVersion);
//                        Logger.Debug("Refreshed item: {ItemKey} in profile: {ProfileId} to version: {Version}", itemKey, profileId, newVersion);
                    }
                }
            }
        }
    }
}
