using NuGet.Editor.Models;
using NuGet.Versioning;

namespace NuGet.Editor.Converter
{
    public class NugetVersionToNugetPackageIdentifier : IConverter<NuGetVersion, NugetPackageIdentifier>
    {
        public NugetPackageIdentifier Convert(NuGetVersion packageSearchMetadata)
        {
            throw new System.NotImplementedException();
        }
    }
}