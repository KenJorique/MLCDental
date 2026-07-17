using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Tasks.v1;
using GoogleTask = Google.Apis.Tasks.v1.Data.Task; // ← alias fixes ambiguity

namespace ClinicApp.Services
{
    public class GoogleTasksService
    {
        private TasksService? _service;
        private bool _isSignedIn = false;

        public static GoogleTasksService Instance { get; } = new();

        public bool IsSignedIn => _isSignedIn;

        public async System.Threading.Tasks.Task<bool> SignInAsync(string accessToken)
        {
            try
            {
                var credential = GoogleCredential
                    .FromAccessToken(accessToken)
                    .CreateScoped(TasksService.Scope.Tasks);

                _service = new TasksService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "MLC Dental Clinic"
                });

                _isSignedIn = true;
                System.Diagnostics.Debug.WriteLine("[GoogleTasks] Signed in");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[GoogleTasks] SignIn failed: {ex.Message}");
                return false;
            }
        }

        public async System.Threading.Tasks.Task<string?> CreateAppointmentTaskAsync(
            string patientName, string service,
            DateTime appointmentDateTime, string phone, string notes = "")
        {
            if (!_isSignedIn || _service == null) return null;
            try
            {
                var task = new GoogleTask  // ← uses alias
                {
                    Title = $"Appointment: {patientName} — {service}",
                    Notes = $"Patient: {patientName}\n" +
                             $"Service: {service}\n" +
                             $"Date: {appointmentDateTime:MMM dd, yyyy h:mm tt}\n" +
                             $"Phone: {phone}\n" +
                             (string.IsNullOrEmpty(notes) ? "" : $"Notes: {notes}"),
                    Due = appointmentDateTime.ToUniversalTime()
                                               .ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    Status = "needsAction"
                };

                var request = _service.Tasks.Insert(task, "@default");
                var created = await request.ExecuteAsync();
                System.Diagnostics.Debug.WriteLine(
                    $"[GoogleTasks] Task created: {created.Id}");
                return created.Id;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[GoogleTasks] CreateTask failed: {ex.Message}");
                return null;
            }
        }

        public async System.Threading.Tasks.Task CompleteTaskAsync(string taskId)
        {
            if (!_isSignedIn || _service == null || string.IsNullOrEmpty(taskId)) return;
            try
            {
                var task = await _service.Tasks.Get("@default", taskId).ExecuteAsync();
                task.Status = "completed";
                await _service.Tasks.Update(task, "@default", taskId).ExecuteAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[GoogleTasks] CompleteTask failed: {ex.Message}");
            }
        }

        public async System.Threading.Tasks.Task DeleteTaskAsync(string taskId)
        {
            if (!_isSignedIn || _service == null || string.IsNullOrEmpty(taskId)) return;
            try
            {
                await _service.Tasks.Delete("@default", taskId).ExecuteAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[GoogleTasks] DeleteTask failed: {ex.Message}");
            }
        }

        public void SignOut()
        {
            _service = null;
            _isSignedIn = false;
        }
    }
}