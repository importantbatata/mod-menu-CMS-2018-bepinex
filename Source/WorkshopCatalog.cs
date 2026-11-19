using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using Steamworks;

namespace CMS18ModMenu
{
    internal sealed class WorkshopEntry
    {
        public readonly ulong Id;
        public readonly string State;
        public string Title;

        public WorkshopEntry(ulong id, string state, string title)
        {
            Id = id;
            State = state;
            Title = title;
        }
    }

    internal sealed class WorkshopCatalog
    {
        private const int BatchSize = 50;
        private readonly ManualLogSource log;
        private readonly Action<bool> changed;
        private readonly Dictionary<ulong, WorkshopEntry> byId = new Dictionary<ulong, WorkshopEntry>();
        private PublishedFileId_t[] ids;
        private int offset;
        private CallResult<SteamUGCQueryCompleted_t> callback;
        private UGCQueryHandle_t query = UGCQueryHandle_t.Invalid;

        public readonly List<WorkshopEntry> Entries = new List<WorkshopEntry>();
        public bool Available { get; private set; }

        public WorkshopCatalog(ManualLogSource log, Action<bool> changed)
        {
            this.log = log;
            this.changed = changed;
        }

        public void Refresh()
        {
            Cancel();
            Entries.Clear();
            byId.Clear();
            Available = false;
            try
            {
                if (SteamManager.Initialized)
                {
                    uint count = SteamUGC.GetNumSubscribedItems();
                    ids = new PublishedFileId_t[count];
                    if (count > 0)
                        Array.Resize(ref ids, (int)SteamUGC.GetSubscribedItems(ids, count));
                    foreach (PublishedFileId_t id in ids)
                    {
                        var state = (EItemState)SteamUGC.GetItemState(id);
                        ulong key = id.m_PublishedFileId;
                        if (byId.ContainsKey(key))
                            continue;
                        var entry = new WorkshopEntry(key, DisplayState(state), LocalTitle(id));
                        byId.Add(key, entry);
                        Entries.Add(entry);
                    }
                    Available = true;
                }
            }
            catch (Exception error)
            {
                log.LogWarning("Could not read Workshop subscriptions: " + error.Message);
                Entries.Clear();
                byId.Clear();
                ids = null;
            }

            changed(true);
            offset = 0;
            if (ids != null && ids.Length > 0)
                RequestBatch();
        }

        private static string DisplayState(EItemState state)
        {
            if ((state & EItemState.k_EItemStateDownloading) != 0)
                return "Downloading";
            if ((state & EItemState.k_EItemStateDownloadPending) != 0)
                return "Download pending";
            if ((state & EItemState.k_EItemStateNeedsUpdate) != 0)
                return "Update pending";
            if ((state & EItemState.k_EItemStateInstalled) != 0)
                return "Installed";
            return "Subscribed";
        }

        private static string LocalTitle(PublishedFileId_t id)
        {
            ulong size;
            uint timestamp;
            string folder;
            if (SteamUGC.GetItemInstallInfo(id, out size, out folder, 4096, out timestamp) && Directory.Exists(folder))
            {
                // CMS18 Workshop downloads often include the author's upload metadata.
                string manifest = Path.Combine(folder, "upload_config.txt");
                try
                {
                    if (File.Exists(manifest) && new FileInfo(manifest).Length < 65536)
                    {
                        foreach (string line in File.ReadAllLines(manifest))
                        {
                            int equals = line.IndexOf('=');
                            if (equals < 0 || !line.Substring(0, equals).Trim().Equals("package_name", StringComparison.OrdinalIgnoreCase))
                                continue;
                            string title = line.Substring(equals + 1).Trim().Trim('"');
                            if (title.Length > 0)
                                return title;
                        }
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return "Workshop item " + id.m_PublishedFileId;
        }

        private void RequestBatch()
        {
            while (ids != null && offset < ids.Length)
            {
                int length = Math.Min(BatchSize, ids.Length - offset);
                var batch = new PublishedFileId_t[length];
                Array.Copy(ids, offset, batch, 0, length);
                offset += length;
                try
                {
                    query = SteamUGC.CreateQueryUGCDetailsRequest(batch, (uint)length);
                    if (query.Equals(UGCQueryHandle_t.Invalid))
                        continue;
                    SteamUGC.SetAllowCachedResponse(query, 3600);
                    SteamAPICall_t request = SteamUGC.SendQueryUGCRequest(query);
                    if (!request.Equals(SteamAPICall_t.Invalid))
                    {
                        if (callback == null)
                            callback = CallResult<SteamUGCQueryCompleted_t>.Create(ReadBatch);
                        callback.Set(request);
                        return;
                    }
                }
                catch (Exception error)
                {
                    log.LogWarning("Could not request Workshop titles: " + error.Message);
                }
                ReleaseQuery();
            }
        }

        private void ReadBatch(SteamUGCQueryCompleted_t result, bool failed)
        {
            try
            {
                if (!failed && result.m_eResult == EResult.k_EResultOK)
                {
                    for (uint index = 0; index < result.m_unNumResultsReturned; index++)
                    {
                        SteamUGCDetails_t details;
                        WorkshopEntry row;
                        if (SteamUGC.GetQueryUGCResult(query, index, out details)
                            && details.m_eResult == EResult.k_EResultOK
                            && !string.IsNullOrEmpty(details.m_rgchTitle)
                            && byId.TryGetValue(details.m_nPublishedFileId.m_PublishedFileId, out row))
                            row.Title = details.m_rgchTitle;
                    }
                    changed(false);
                }
            }
            catch (Exception error)
            {
                log.LogWarning("Could not read Workshop titles: " + error.Message);
            }
            finally
            {
                ReleaseQuery();
            }
            RequestBatch();
        }

        private void ReleaseQuery()
        {
            if (query.Equals(UGCQueryHandle_t.Invalid))
                return;
            try
            {
                if (SteamManager.Initialized)
                    SteamUGC.ReleaseQueryUGCRequest(query);
            }
            catch (Exception error)
            {
                log.LogWarning("Could not release Workshop request: " + error.Message);
            }
            query = UGCQueryHandle_t.Invalid;
        }

        public void Cancel()
        {
            if (callback != null)
            {
                callback.Dispose();
                callback = null;
            }
            ReleaseQuery();
            ids = null;
        }
    }
}
