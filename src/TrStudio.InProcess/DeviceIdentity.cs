namespace TrStudio.InProcess;

/// <summary>
/// This computer's device id for AppManager (<c>X-Device-Id</c>, 1 to 200 characters). Made once, at
/// random, and kept in the user's data folder, so every start of the same library names the same
/// device. It identifies the install, not the person, and holds nothing secret.
/// </summary>
public static class DeviceIdentity
{
    /// <summary>The file in the data folder that holds the id.</summary>
    public const string FileName = "device-id.txt";

    /// <summary>Reads the kept id, or makes and keeps a new one. An unwritable folder gets an id for this run only.</summary>
    /// <param name="aDataFolder">The user's TrStudio data folder.</param>
    /// <returns>A 32-character id.</returns>
    public static string ReadOrCreate(string aDataFolder)
    {
        var vPath = Path.Combine(aDataFolder, FileName);
        try
        {
            if (File.Exists(vPath))
            {
                var vKept = File.ReadAllText(vPath).Trim();
                if (vKept.Length is >= 1 and <= 200)
                {
                    return vKept;
                }
            }

            var vNew = "trstudio-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(aDataFolder);
            File.WriteAllText(vPath, vNew);
            return vNew;
        }
        catch (Exception vException) when (vException is IOException or UnauthorizedAccessException)
        {
            // Setup required reports the folder problem itself (REQ-UI-102); sign-in still names a device.
            return "trstudio-" + Guid.NewGuid().ToString("N");
        }
    }
}
