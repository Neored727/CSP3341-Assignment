using StudentRecordSystem.Core;

namespace StudentRecordSystem.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        // One writer per Windows user prevents two instances overwriting the same JSON file.
        string profile = Environment.UserName;
        using var mutex = new Mutex(true, "Local\\NeoStudentRecords-" + profile, out bool first);
        if (!first)
        {
            MessageBox.Show("Student Records is already running for this Windows user.", "Already open");
            return;
        }
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NeoStudentRecords");
        string path = Path.Combine(folder, "school-data.json");
        try
        {
            var store = new JsonSchoolStore(path);
            var data = store.LoadAsync().GetAwaiter().GetResult();
            if (data is null)
            {
                data = DemoData.Create();
                store.SaveAsync(data).GetAwaiter().GetResult();
            }
            var service = new SchoolService(data, store);
            Application.Run(new AppShell(service, data.SuggestedAsOf));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"The application could not open its records.\n\n{ex.Message}\n\n" +
                $"Data location:\n{path}\n\nIf present, school-data.json.bak contains the previous saved version. " +
                "Keep a copy of the current file before restoring a backup.", "Unable to open Student Records", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
