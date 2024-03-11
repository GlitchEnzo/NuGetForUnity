using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace NugetForUnity.Configuration
{
    [Serializable]
    internal class NativeRuntimeAssetConfiguration : ISerializationCallbackReceiver
    {
        [SerializeField]
        private string cpuArchitecture;

        [SerializeField]
        private string editorCpuArchitecture;

        [SerializeField]
        private string editorOperatingSystem;

        [SerializeField]
        private string runtime;

        [SerializeField]
        private List<string> supportetPlatformTargets;

        /// <summary>
        ///     Initializes a new instance of the <see cref="NativeRuntimeAssetConfiguration" /> class.
        ///     This constructor is only for deserializer.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public NativeRuntimeAssetConfiguration()
        {
        }

        public NativeRuntimeAssetConfiguration(
            [NotNull] string runtime,
            [CanBeNull] string cpuArchitecture,
            [CanBeNull] string editorCpuArchitecture,
            [CanBeNull] string editorOperatingSystem,
            [NotNull] params BuildTarget[] supportetPlatformTargets)
        {
            this.runtime = runtime;
            this.cpuArchitecture = cpuArchitecture;
            SupportetPlatformTargets = supportetPlatformTargets.ToList();
            this.editorCpuArchitecture = editorCpuArchitecture;
            this.editorOperatingSystem = editorOperatingSystem;
            SetNullStringsToEmpty();
        }

        [NotNull]
        public string Runtime
        {
            get => runtime;
            set => runtime = value;
        }

        [field: NonSerialized]
        [NotNull]
        public List<BuildTarget> SupportetPlatformTargets { get; set; }

        [CanBeNull]
        public string CpuArchitecture
        {
            get => cpuArchitecture;
            set => cpuArchitecture = value;
        }

        [CanBeNull]
        public string EditorCpuArchitecture
        {
            get => editorCpuArchitecture;
            set => editorCpuArchitecture = value;
        }

        [CanBeNull]
        public string EditorOperatingSystem
        {
            get => editorOperatingSystem;
            set => editorOperatingSystem = value;
        }

        /// <inheritdoc />
        public void OnAfterDeserialize()
        {
            if (SupportetPlatformTargets is null)
            {
                SupportetPlatformTargets = new List<BuildTarget>();
            }
            else
            {
                SupportetPlatformTargets.Clear();
            }

            foreach (var target in supportetPlatformTargets)
            {
                if (Enum.TryParse<BuildTarget>(target, true, out var parsedTarget))
                {
                    SupportetPlatformTargets.Add(parsedTarget);
                }
            }

            SetNullStringsToEmpty();
        }

        /// <inheritdoc />
        public void OnBeforeSerialize()
        {
            supportetPlatformTargets = SupportetPlatformTargets.ConvertAll(target => target.ToString());
        }

        private void SetNullStringsToEmpty()
        {
            if (editorOperatingSystem is null)
            {
                editorOperatingSystem = string.Empty;
            }

            if (cpuArchitecture is null)
            {
                cpuArchitecture = string.Empty;
            }

            if (editorCpuArchitecture is null)
            {
                editorCpuArchitecture = string.Empty;
            }
        }
    }
}
