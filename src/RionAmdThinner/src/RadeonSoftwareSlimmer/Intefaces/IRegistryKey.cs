using System;
using Microsoft.Win32;

namespace RadeonSoftwareSlimmer.Intefaces
{
    public interface IRegistryKey : IDisposable
    {
        string Name { get; }


        IRegistryKey OpenSubKey(string name, bool writable);

        IRegistryKey CreateSubKey(string name);

        string[] GetSubKeyNames();

        string[] GetValueNames();


        object GetValue(string name);

        object GetValue(string name, object defaultValue);

        RegistryValueKind GetValueKind(string name);

        void SetValue(string name, object value, RegistryValueKind valueKind);

        void DeleteValue(string name, bool throwOnMissingValue);
    }
}
