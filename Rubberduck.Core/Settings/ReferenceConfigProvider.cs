﻿using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Rubberduck.Resources.Registration;
using Rubberduck.SettingsProvider;
using Rubberduck.UI;
using Rubberduck.VBEditor;
using Rubberduck.VBEditor.Events;
using Rubberduck.VBEditor.SafeComWrappers;

namespace Rubberduck.Settings
{
    public class ReferenceConfigProvider : IConfigProvider<ReferenceSettings>, IDisposable
    {
        private static readonly string HostApplication = Path.GetFileName(Application.ExecutablePath).ToUpperInvariant();

        private readonly IPersistanceService<ReferenceSettings> _persister;
        private readonly IEnvironmentProvider _environment;
        private readonly IVBEEvents _events;
        private bool _listening;

        public ReferenceConfigProvider(IPersistanceService<ReferenceSettings> persister, IEnvironmentProvider environment, IVBEEvents events)
        {
            _persister = persister;
            _environment = environment;
            _events = events;

            
            var settings = Create();
            _listening = settings.AddToRecentOnReferenceEvents;
            if (_listening && _events != null)
            {
                _events.ProjectReferenceAdded += ReferenceAddedHandler;
            }
        }

        public ReferenceSettings Create()
        {
            var defaults = CreateDefaults();
            return _persister.Load(defaults) ?? defaults;
        }

        public ReferenceSettings CreateDefaults()
        {
            var defaults = new ReferenceSettings
            {
                RecentReferencesTracked = 20
            };

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            defaults.PinReference(new ReferenceInfo(new Guid(RubberduckGuid.RubberduckTypeLibGuid), string.Empty, string.Empty, version.Major, version.Minor));
            defaults.PinReference(new ReferenceInfo(new Guid(RubberduckGuid.RubberduckApiTypeLibGuid), string.Empty, string.Empty, version.Major, version.Minor));

            var documents = _environment?.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrEmpty(documents))
            {
                defaults.ProjectPaths.Add(documents);
            }

            var appdata = _environment?.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(documents))
            {
                var addins = Path.Combine(appdata, "Microsoft", "AddIns");
                if (Directory.Exists(addins))
                {
                    defaults.ProjectPaths.Add(addins);
                }

                var templates = Path.Combine(appdata, "Microsoft", "Templates");
                if (Directory.Exists(templates))
                {
                    defaults.ProjectPaths.Add(templates);
                }
            }

            return defaults;
        }

        public void Save(ReferenceSettings settings)
        {
            if (_listening && _events != null && !settings.AddToRecentOnReferenceEvents)
            {
                _events.ProjectReferenceAdded -= ReferenceAddedHandler;
                _listening = false;
            }

            if (_listening && _events != null && !settings.AddToRecentOnReferenceEvents)
            {
                _events.ProjectReferenceAdded += ReferenceAddedHandler;
                _listening = true;
            }

            _persister.Save(settings);
        }

        private void ReferenceAddedHandler(object sender, ReferenceEventArgs e)
        {
            if (e is null || e.Reference.Equals(ReferenceInfo.Empty))
            {
                return;
            }

            var settings = Create();
            settings.TrackUsage(e.Reference, e.Type == ReferenceKind.Project ? HostApplication : null);
            _persister.Save(settings);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool _disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing && _listening)
            {
                _events.ProjectReferenceAdded -= ReferenceAddedHandler;
            }

            _disposed = true;
        }
    }
}
