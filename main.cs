using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InstrumentCalibrationTracker
{
    class Program
    {
        // Move email credentials to configuration files or environment variables.
        private static readonly string EmailUsername = "your_email@gmail.com";
        private static readonly string EmailPassword = "your_email_password";

        static async Task Main(string[] args)
        {
            string connectionString = @"Data Source=YourServer;Initial Catalog=CalibrationDatabase;" +
                "Integrated Security=True;MultipleActiveResultSets=True";

            try
            {
                using SqlConnection conn = new SqlConnection(connectionString);
                await conn.OpenAsync();

                await CheckCalibrationRemindersAsync(conn);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }

            Console.ReadKey();
        }

        static async Task CheckCalibrationRemindersAsync(SqlConnection connection)
        {
            try
            {
                // Query the database to retrieve instruments that need calibration.
                string query = "SELECT * FROM Instruments WHERE NextCalibrationDate <= GETDATE()";
                using (var command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string instrumentName = reader["InstrumentName"].ToString();
                            DateTime nextCalibrationDate = Convert.ToDateTime(reader["NextCalibrationDate"]);

                            // Calculate days until calibration is due.
                            int daysUntilDue = (int)(nextCalibrationDate - DateTime.Now).TotalDays;

                            if (daysUntilDue <= 7)
                            {
                                string userEmail = reader["UserEmail"].ToString();
                                string reminderSubject = "Instrument Calibration Reminder";
                                string reminderBody = $"Reminder: {instrumentName} calibration is due in {daysUntilDue} days.";

                                // Send email reminders.
                                await SendEmailAsync(userEmail, reminderSubject, reminderBody);

                                // Update the LastReminderSentDate in the database to avoid duplicate reminders.
                                await UpdateLastReminderDateAsync(connection, instrumentName);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while checking reminders: {ex.Message}");
            }
        }

        // Modify the SendEmail method to use async and secure the email credentials.
        public static async Task SendEmailAsync(string emailTo, string subject, string body)
        {
            try
            {
                using (SmtpClient client = new SmtpClient("smtp.gmail.com"))
                {
                    client.Port = 587;
                    client.EnableSsl = true;
                    client.Timeout = 10000;
                    client.DeliveryMethod = SmtpDeliveryMethod.Network;
                    client.UseDefaultCredentials = false;

                    // Use SecureString for password to enhance security.
                    SecureString securePassword = new SecureString();
                    foreach (char c in EmailPassword)
                    {
                        securePassword.AppendChar(c);
                    }
                    client.Credentials = new NetworkCredential(EmailUsername, securePassword);

                    MailMessage mail = new MailMessage(EmailUsername, emailTo);
                    mail.Subject = subject;
                    mail.Body = body;

                    await client.SendMailAsync(mail);
                    Console.WriteLine($"Email sent to {emailTo}: {subject}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Email sending error: {ex.Message}");
            }
        }

        // Update the LastReminderSentDate in the database.
        static async Task UpdateLastReminderDateAsync(SqlConnection connection, string instrumentName)
        {
            try
            {
                string query = "UPDATE Instruments SET LastReminderSentDate = GETDATE() WHERE InstrumentName = @InstrumentName";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@InstrumentName", instrumentName);

                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database update error: {ex.Message}");
            }
        }
    }
}