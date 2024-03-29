using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace NugetForUnity.Configuration
{
    /// <summary>
    ///     Serializable configuration about how native runtime assets (.dll's) are imported.
    /// </summary>
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
        private List<string> supportedPlatformTargets;

        /// <summary>
        ///     Initializes a new instance of the <see cref="NativeRuntimeAssetConfiguration" /> class.
        ///     This constructor is only for deserializer.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public NativeRuntimeAssetConfiguration()
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="NativeRuntimeAssetConfiguration" /> class.
        /// </summary>
        /// <param name="runtime">The name of the runtime, the name of the folder inside the runtimes/native folder, for witch this configuration is used.</param>
        /// <param name="cpuArchitecture">The cpu architecture of the target device for witch assets of this runtime should be included in the build.</param>
        /// <param name="editorCpuArchitecture">The cpu architecture of the Unity Editor on witch the assets should be included.</param>
        /// <param name="editorOperatingSystem">The name of the operating system running the Unity Editor on witch the assets should be used in.</param>
        /// <param name="supportedPlatformTargets">The target platforms for witch this asset should be included in the build.</param>
        public NativeRuntimeAssetConfiguration(
            [NotNull] string runtime,
            [CanBeNull] string cpuArchitecture,
            [CanBeNull] string editorCpuArchitecture,
            [CanBeNull] string editorOperatingSystem,
            [NotNull] params BuildTarget[] supportedPlatformTargets)
        {
            this.runtime = runtime;
            this.cpuArchitecture = cpuArchitecture;
            SupportedPlatformTargets = supportedPlatformTargets.ToList();
            this.editorCpuArchitecture = editorCpuArchitecture;
            this.editorOperatingSystem = editorOperatingSystem;
            SetNullStringsToEmpty();
        }

        /// <summary>
        ///     Gets or sets the name of the runtime, the name of the folder inside the runtimes/native folder, for witch this configuration is used.
        /// </summary>
        [NotNull]
        public string Runtime
        {
            get => runtime;
            set => runtime = value;
        }

        /// <summary>
        ///     Gets or sets the target platforms for witch assets of this runtime should be included in the build.
        /// </summary>
        [field: NonSerialized]
        [NotNull]
        public List<BuildTarget> SupportedPlatformTargets { get; set; }

        /// <summary>
        ///     Gets or sets the cpu architecture of the target device for witch assets of this runtime should be included in the build.
        ///     If this is null / empty the assets are included for all cpu architectures.
        /// </summary>
        [CanBeNull]
        public string CpuArchitecture
        {
            get => cpuArchitecture;
            set => cpuArchitecture = value;
        }

        /// <summary>
        ///     Gets or sets the cpu architecture of the Unity Editor on witch the assets should be included.
        ///     If this is null / empty the asset is used by all Unity Editors.
        /// </summary>
        [CanBeNull]
        public string EditorCpuArchitecture
        {
            get => editorCpuArchitecture;
            set => editorCpuArchitecture = value;
        }

        /// <summary>
        ///     Gets or sets the name of the operating system running the Unity Editor on witch the assets should be used in.
        ///     If this is null the assets are only used in Runtime.
        /// </summary>
        [CanBeNull]
        public string EditorOperatingSystem
        {
            get => editorOperatingSystem;
            set => editorOperatingSystem = value;
        }

        /// <inheritdoc />
        public void OnAfterDeserialize()
        {
            if (SupportedPlatformTargets is null)
            {
                SupportedPlatformTargets = new List<BuildTarget>();
            }
            else
            {
                SupportedPlatformTargets.Clear();
            }

            foreach (var target in supportedPlatformTargets)
            {
                if (Enum.TryParse<BuildTarget>(target, true, out var parsedTarget))
                {
                    SupportedPlatformTargets.Add(parsedTarget);
                }
            }

            SetNullStringsToEmpty();
        }

        /// <inheritdoc />
        public void OnBeforeSerialize()
        {
            supportedPlatformTargets = SupportedPlatformTargets.ConvertAll(target => target.ToString());
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
