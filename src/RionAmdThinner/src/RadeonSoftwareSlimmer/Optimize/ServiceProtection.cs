using System;
using System.Collections.Generic;

namespace RadeonSoftwareSlimmer.Optimize
{
    /// <summary>Shared exclusions for bulk trimming and the startup service controls.</summary>
    public static class ServiceProtection
    {
        private static readonly Dictionary<string, string> Reasons = Build();

        private static Dictionary<string, string> Build()
        {
            var rules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            void Add(string reason, string names)
            {
                foreach (string name in names.Split(' ')) rules.Add(name, reason);
            }
            Add("Wi-Fi, Ethernet, VPN and network discovery", "WlanSvc dot3svc Dhcp Dnscache nsi NlaSvc netprofm Netman Wcmsvc WinHttpAutoProxySvc iphlpsvc EapHost RasMan RasAuto SstpSvc IKEEXT PolicyAgent LanmanWorkstation LanmanServer lmhosts Netlogon NcbService WwanSvc WFDSConMgrSvc icssvc SharedAccess wcncsvc SSDPSRV upnphost fdPHost FDResPub lltdsvc RemoteAccess");
            Add("Bluetooth and device pairing", "bthserv BluetoothUserService BthAvctpSvc BTAGService BthHFSrv DeviceAssociationService DeviceAssociationBrokerSvc DevicePickerUserSvc DevicesFlowUserSvc CDPSvc CDPUserSvc");
            Add("Audio and microphone", "Audiosrv AudioEndpointBuilder MMCSS NPSMSvc");
            Add("Windows Search", "WSearch");
            Add("Shadow Copy, backup and restore points", "VSS swprv SDRSVC wbengine SystemEventsBroker Schedule Winmgmt EventSystem COMSysApp CloudBackupRestoreSvc FileHistorySvc fhsvc");
            Add("Windows core, sign-in, storage and hardware", "RpcSs RpcEptMapper DcomLaunch Power PlugPlay UserManager ProfSvc SamSs LSM EventLog gpsvc SENS BrokerInfrastructure CoreMessagingRegistrar StateRepository Appinfo AppReadiness AppXSvc ClipSVC InstallService CryptSvc KeyIso VaultSvc NgcSvc NgcCtnrSvc WbioSrvc SCardSvr ScDeviceEnum CertPropSvc BDESVC StorSvc vds ShellHWDetection hidserv TextInputManagementService TabletInputService DispBrokerDesktopSvc DisplayEnhancementService FontCache Themes WpnService WpnUserService TokenBroker LicenseManager AppIDSvc camsvc");
            Add("Security, firewall and updates", "BFE MpsSvc WinDefend WdNisSvc SecurityHealthService wscsvc Sense webthreatdefsvc webthreatdefusersvc wuauserv UsoSvc WaaSMedicSvc BITS TrustedInstaller DoSvc uhssvc W32Time");
            Add("Laptop power, thermal control and OEM hardware support", "DptfPolicyLpmService DptfParticipantProcessorService esifsvc ipfsvc ipf_uf IntelDTT IntelPMT AMDPMF AMDRyzenMasterDriverV22 AsusOptimization ASUSSystemControlService LenovoVantageService ImControllerService EnergyServerService queencreek");
            Add("Game input, installation and anti-cheat", "GameInputSvc GameInputRedistService GamingServices GamingServicesNet EpicOnlineServices vgc vgk BEService BEService_x64 EasyAntiCheat EasyAntiCheat_EOS EAAntiCheatService FACEITService FaceitServiceService EQU8 AntiCheatExpertService ACE-GAME");
            return rules;
        }

        public static string Reason(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Unknown service identity";
            if (Reasons.TryGetValue(name, out string reason)) return reason;
            // Windows creates suffixed instances of per-user service templates.
            int suffix = name.LastIndexOf('_');
            return suffix > 0 && Reasons.TryGetValue(name.Substring(0, suffix), out reason) ? reason : null;
        }
    }
}
