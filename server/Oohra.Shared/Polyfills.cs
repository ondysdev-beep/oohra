// Doplněk pro .NET Standard 2.1 (Unity): record potřebuje typ IsExternalInit, který tam chybí.
#if NETSTANDARD2_1
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
#endif
