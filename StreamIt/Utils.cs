using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Com.Josh2112.StreamIt
{
    public static class Utils
    {
        public static FileVersionInfo AssemblyInfo => FileVersionInfo.GetVersionInfo( Assembly.GetEntryAssembly()!.Location );

        public static readonly Lazy<string> DataPath = new( () => Directory.CreateDirectory(
            Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ),
                AssemblyInfo.CompanyName!, AssemblyInfo.ProductName! ) ).FullName );

        public static string? GetFileExtensionForMimeType( string mimeType ) =>
            Registry.ClassesRoot.OpenSubKey( @"MIME\Database\Content Type\" + mimeType, false )?.GetValue( "Extension", null )?.ToString();
    }
}
