using System;
using Microsoft.Win32;
using RadeonSoftwareSlimmer.Intefaces;

namespace RadeonSoftwareSlimmer.Services
{
    public class WindowsRegistryKey : IRegistryKey
    {
        private readonly RegistryKey _regKey;
        private bool disposedValue;


        public WindowsRegistryKey(RegistryKey registryKey)
        {
            if (registryKey == null)
                throw new ArgumentNullException(nameof(registryKey), "Registry key cannot be null.");

            _regKey = registryKey;
        }


        public string Name => _regKey.Name;


        public object GetValue(string name)
        {
            return _regKey.GetValue(name);
        }

        public object GetValue(string name, object defaultValue)
        {
            return _regKey.GetValue(name, defaultValue);
        }

        internal object GetRawValue(string name) => _regKey.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);

        public RegistryValueKind GetValueKind(string name)
        {
            return _regKey.GetValueKind(name);
        }

        public void SetValue(string name, object value, RegistryValueKind valueKind)
        {
            _regKey.SetValue(name, value, valueKind);
        }

        public void DeleteValue(string name, bool throwOnMissingValue)
        {
            _regKey.DeleteValue(name, throwOnMissingValue);
        }

        public string[] GetValueNames()
        {
            return _regKey.GetValueNames();
        }


        public IRegistryKey OpenSubKey(string name, bool writable)
        {
            RegistryKey openKey = _regKey.OpenSubKey(name, writable);

            if (openKey == null)
                return null;
            else
                return new WindowsRegistryKey(openKey);
        }

        public IRegistryKey CreateSubKey(string name)
        {
            return new WindowsRegistryKey(_regKey.CreateSubKey(name, true));
        }

        public string[] GetSubKeyNames()
        {
            return _regKey.GetSubKeyNames();
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects)
                    if (_regKey != null)
                        _regKey.Dispose();
                }

                // free unmanaged resources (unmanaged objects) and override finalizer
                // set large fields to null
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}

