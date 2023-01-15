#nullable enable

namespace UnityEngine
{
    internal interface ISerializationCallbackReceiver
    {
        void OnBeforeSerialize();

        void OnAfterDeserialize();
    }
}
