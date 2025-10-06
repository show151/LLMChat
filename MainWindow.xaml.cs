using System.Windows;
using Microsoft.Data.Sqlite;
using System.IO;
using Mscc.GenerativeAI;

namespace LLMChat;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private GoogleAI googleAI;
    private GenerativeModel model;

    public MainWindow()
    {
        InitializeComponent();

        // Set the API key for the generative model (if required by your library)
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "API.txt");
        Environment.SetEnvironmentVariable("GOOGLE_API_KEY", File.ReadAllText(filePath).Trim());

        // Initialize the generative model
        googleAI = new GoogleAI();
        model = googleAI.GenerativeModel(
            Model.Gemini25Flash
        );

        // Initialize SQLite database
        string dbPath = "chat.db";
        if (!File.Exists(dbPath))
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE Conversation (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT,
                    UserMessage TEXT,
                    BotResponse TEXT
                );";
            command.ExecuteNonQuery();
        }

    }

    public class ConversationEntry
    {
        public int Id { get; set; }
        public string Timestamp { get; set; }
        public string UserMessage { get; set; }
        public string BotResponse { get; set; }
    }

    public void LoadConversationHistory()
    {
        var entries = new List<ConversationEntry>();

        using var connection = new SqliteConnection("Data Source=chat.db");
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Timestamp, UserMessage, BotResponse FROM Conversation";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            entries.Add(new ConversationEntry
            {
                Id = reader.GetInt32(0),
                Timestamp = reader.GetString(1),
                UserMessage = reader.GetString(2),
                BotResponse = reader.GetString(3)
            });
        }

        ChatDataGrid.ItemsSource = entries;
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Get the user's message from the input box
            string userMessage = InputTextBox.Text;
            var response = await model.GenerateContent(userMessage);

            // Here you would typically call your LLM API to get a response
            string botResponse = response.Text;
            ResponseTextBox.Text = botResponse;

            // Clear the input box after sending the message
            InputTextBox.Clear();

            // Save the conversation to the SQLite database
            using var connection = new SqliteConnection("Data Source=chat.db");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
            INSERT INTO Conversation (Timestamp, UserMessage, BotResponse)
            VALUES ($timestamp, $userMessage, $botResponse);";
            command.Parameters.AddWithValue("$timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            command.Parameters.AddWithValue("$userMessage", userMessage);
            command.Parameters.AddWithValue("$botResponse", botResponse);
            command.ExecuteNonQuery();

            LoadConversationHistory();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[エラー] {ex.Message}");
            MessageBox.Show("エラーが発生しました：" + ex.Message);
            return;
        }
    }
}