using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace UserSide
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string ipAddress;
        private int port;
        private string username;
        private int timeLimit;


        private TcpClient client;
        private NetworkStream stream;
        private StreamReader reader;
        private StreamWriter writer;
        private int totalWords;

        private int numCorrectGuess;
        private int numIncorrectGuess;
        private bool isServerShuttingDown = false;

        private DispatcherTimer timer;
        private int remainingTime;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Read values from textboxes
                ipAddress = txtIpAddress.Text;
                port = int.Parse(txtPort.Text);

                // Connect to the server asynchronously
                client = new TcpClient();
                await client.ConnectAsync(ipAddress, port);

                connectionGrid.Visibility = Visibility.Collapsed;
                gameGrid.Visibility = Visibility.Visible;
                stream = client.GetStream();
                reader = new StreamReader(stream);
                writer = new StreamWriter(stream);
                // Read the 80-character string from the server
                byte[] buffer = new byte[80];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                string serverString = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                // Read the number of words from the server
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                totalWords = int.Parse(Encoding.ASCII.GetString(buffer, 0, bytesRead));
                //Before updating to UI remove whnitespaces between the words to confuse the clients
                serverString = serverString.Replace(" ", string.Empty);
                // Update UI with the received information
                secretWordLabel.Text = "Words to Guess: " + serverString;
                totalWordsLabel.Text = "Total Numbers: " + totalWords;


                // Initialize and start the timer
                timeLimit = int.Parse(txtTimeLimit.Text);
                remainingTime = timeLimit;
                timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(1);
                timer.Tick += Timer_Tick;
                timer.Start();
            }

            catch (Exception ex)
            {
                // Handle connection error

                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            remainingTime--;

            if (remainingTime <= 0)
            {
                timer.Stop();
                MessageBox.Show("Oooo....Time's up!", "Game Completed", MessageBoxButton.OK);
                writer.WriteLine("endgame");
                Application.Current.Shutdown();
            }
        }




        private async void SubmitGuess_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string userGuess = txtGuess.Text;

                //Send the user;s guess to the server
                await writer.WriteLineAsync(userGuess);
                await writer.FlushAsync();

                //Receive and process the user guess
                string response = await reader.ReadLineAsync();

                //Update the UI 
                UpdateUI(response);

            }

            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private async void UpdateUI(string response)
        {
            Dispatcher.Invoke(() =>
            {
                if (response != null && response.StartsWith("Server is shutting down."))
                {
                    // Handle server shutdown gracefully
                    isServerShuttingDown = true;
                    MessageBox.Show("The server is shutting down. The game will now exit.");
                    Application.Current.Shutdown();
                }
                if (response != null && response.StartsWith("Correct!"))
                {
                    numCorrectGuess++;
                    correctStatus.Text = response;
                    alreadyGuessedStatus.Text = "";
                }

                if (response != null && response.StartsWith("Note:"))
                {
                    alreadyGuessedStatus.Text = response;
                }

                if (response != null && response.StartsWith("Incorrect!"))
                {
                    numIncorrectGuess++;
                    incorrectStatus.Text = response + " Incorrect Count: " + numIncorrectGuess.ToString();
                    alreadyGuessedStatus.Text = "";
                }

                if (response != null && response.StartsWith("Congratulations!"))
                {
                   
                    MessageBoxResult result = MessageBox.Show("Congratulations! You've found all the words!\nDo you want to play again?", "Game Completed", MessageBoxButton.YesNo);
                    if (result == MessageBoxResult.Yes)
                    {
                        StartNewGame();
                    }

                    else
                    {
                        SendEndGameRequest();
                    }
                }

            });
        }


        private async void StartNewGame()
        {
            correctStatus.Text = "";
            incorrectStatus.Text = "";
            alreadyGuessedStatus.Text = "";

            try
            {
                writer.WriteLine("yes");
                writer.Flush();

                // Check if the client is still connected
                if (client.Connected)
                {
                    byte[] buffer = new byte[80];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                    // Check if the client is still connected after reading from the stream
                    if (client.Connected)
                    {
                        string serverString = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                        // Read the number of words from the server
                        bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        totalWords = int.Parse(Encoding.ASCII.GetString(buffer, 0, bytesRead));

                        // Before updating to UI remove whitespaces between the words to confuse the clients
                        serverString = serverString.Replace(" ", string.Empty);

                        // Update UI with the received information
                        secretWordLabel.Text = "Words to Guess: " + serverString;
                        totalWordsLabel.Text = "Total Numbers: " + totalWords;
                    }
                    else
                    {
                        // Handle the case where the client is no longer connected (e.g., the application is closed)
                        MessageBox.Show("Connection closed. Game cannot be started.");
                    }

                    // Restart the timer for the new game
                    remainingTime = timeLimit;
                    timer.Start();
                }
                else
                {
                    // Handle the case where the client is no longer connected (e.g., the application is closed)
                    MessageBox.Show("Connection closed. Game cannot be started.");
                }
            }
            catch (Exception ex)
            {
                // Handle any other exceptions
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void EndGame_Click(object sender, RoutedEventArgs e)
        {

            SendEndGameRequest();
        }

        private async void SendEndGameRequest()
        {
            try
            {
                MessageBoxResult result = MessageBox.Show("Are you sure you want to end the game?", "Confirm End Game", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                {
                    // User confirmed, send a request to the server
                    await writer.WriteLineAsync("endgame");
                    await writer.FlushAsync();
                    Application.Current.Shutdown();
                }
                else
                {
                    return;
                }
                
            }

            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void Populate_Click(object sender, RoutedEventArgs e)
        {
            txtIpAddress.Text = "52.151.251.103";
            txtPort.Text = "1111";
            txtUserName.Text = "Bhuwan";
            txtTimeLimit.Text = "50";
        }
    }
}
