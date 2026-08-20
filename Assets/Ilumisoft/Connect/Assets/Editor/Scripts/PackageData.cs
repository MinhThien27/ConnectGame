namespace Ilumisoft.Connect.Editor.GameTemplate
{
    using UnityEngine;

    public class PackageData : ScriptableObject
    {
        public string PackageID = string.Empty;

        public string URL => $"https://assetstore.unity.com/packages/slug/{PackageID}?aid=1100l9P3D";

        public string Name => name;
    }
}