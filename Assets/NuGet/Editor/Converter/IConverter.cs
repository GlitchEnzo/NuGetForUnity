namespace NuGet.Editor.Converter
{
    public interface IConverter <TFrom, TTo>
    {
        TTo Convert(TFrom packageSearchMetadata);
    }
}