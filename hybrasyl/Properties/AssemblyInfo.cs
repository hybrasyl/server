using System.Reflection;
using System.Runtime.InteropServices;

// Log4Net configuration
[assembly: log4net.Config.XmlConfigurator(ConfigFile = "Log4Net.config", Watch = true)]

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Hybrasyl")]
[assembly: AssemblyDescription("Hybrasyl, a DOOMVAS v1 emulator")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Project Hybrasyl")]
[assembly: AssemblyProduct("Hybrasyl Server")]
[assembly: AssemblyCopyright("(C) 2016 Project Hybrasyl")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("f9c7d297-b8aa-4d3e-94a3-34144f974f1a")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("0.6.2.*")]
[assembly: AssemblyFileVersion("0.6.2.0")]
