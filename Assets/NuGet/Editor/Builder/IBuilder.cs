namespace NuGet.Editor
{
    public interface IBuilder<T>
    {
        T build();
    }
}