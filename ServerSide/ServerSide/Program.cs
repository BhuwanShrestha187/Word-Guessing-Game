using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Configuration; 
namespace ServerSide
{
    internal class Program
    {
        static string directoryPath = "D:\\Conestoga College";
        static List<string> wordsToFind;
        static TcpListener server;
        static bool isClientClosed = false;
        

        static readonly object lockObject = new object();

        // Use ManualResetEvent to signal the shutdown
        static ManualResetEvent shutdownEvent = new ManualResetEvent(false);


        static void Main(string[] args)
        {
            string ipAddress = ConfigurationManager.AppSettings["ipAddress"];
            server = null; 
            try
            {
                //Set the TcpListener on port 1111
                Int32 port = 1111; 
                IPAddress localAddr = IPAddress.Parse(ipAddress);
                server = new TcpListener(localAddr, port);
                server.Start();
                while (!isClientClosed)
                {
                    
                    try
                    {
                        TcpClient client = server.AcceptTcpClient();
                        // Create a new thread for each client
                        Thread clientThread = new Thread(() => HandleClient(client));
                        clientThread.Start();
                    }
                    catch (SocketException)
                    {
                        // SocketException will be thrown when the server is stopped
                        break;
                    }
                }
            }
            finally
            {
                // Set the shutdown event and wait for all clients to finish
                shutdownEvent.Set();
                
            }
            server.Stop();
        }

        static void HandleClient(TcpClient client)
        {
            StreamReader reader = new StreamReader(client.GetStream());
            StreamWriter writer = new StreamWriter(client.GetStream());

            try
            {
                if (client != null)
                {
                    List<string> guessedWord = new List<string>(); 

                    while (true)
                    {   
                        string textFilePath = ChooseTextFile(directoryPath);
                        string[] lines = File.ReadAllLines(textFilePath);
                        string characterString;
                        int numberOfWords;

                        // Ensure that the file has at least two lines
                        if (lines.Length >= 2)
                        {
                            // Extract information from the file
                             characterString = lines[0].Substring(0, Math.Min(lines[0].Length, 80));
                             numberOfWords = CountWords(lines[0]); 
                            // Send information to the client
                            writer.WriteLine(characterString);
                            writer.WriteLine(numberOfWords);
                            
                            writer.Flush(); // Ensure that the data is sent immediately

                            // You may add additional logic here to send the list of words if needed
                           wordsToFind = lines[0].Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                            int correctWords = 0;
                            // Notify the user and get their guesses
                            while (numberOfWords > 0)
                            {
                                string userGuess = reader.ReadLine();
                               
                                if (userGuess.StartsWith("endgame"))
                                {
                                    writer.WriteLine("Ended Communication with the server!!. You guessed " + correctWords.ToString() + " words correct!!");
                                    writer.Flush(); 
                                    
                                    return;        
                                }

                                // Check if the user guess is in the list of words
                                      if(wordsToFind.Contains(userGuess))
                                        {
                                            guessedWord.Add(userGuess);
                                            numberOfWords--;
                                            if (numberOfWords > 0)
                                            {
                                                writer.WriteLine($"Correct! {numberOfWords} word(s) still to find.");
                                                correctWords++;
                                            }

                                            else
                                            {
                                                writer.WriteLine("Congratulations! You've found all the words!");
                                                writer.Flush();

                                                break;
                                            }
                                        }
                                        else
                                        {

                                    if (guessedWord.Contains(userGuess))
                                    {
                                        writer.WriteLine("Note: You guessed this number already!!");
                                        writer.Flush();
                                        continue;
                                    }

                                    writer.WriteLine("Incorrect! Try Again!!");
                                        }
                                    
                                

                                    writer.Flush();  
                            }                               
                        }
                        else
                        {
                            writer.WriteLine("Invalid format of the text file.");
                        }

                        string playAgainResponse = reader.ReadLine();
                        if (playAgainResponse.Equals("yes", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;

                        }
                        else
                        {

                            client.Close();
                            break;
                            
                        }

                    }  
                   
                }
                else
                {
                    writer.WriteLine("Client is null");
                }
            }
            catch (Exception ex)
            {
                writer.WriteLine($"Error handling client: {ex.Message}");
            }
            finally
            {
                writer.Close();
                reader.Close();
                client.Close(); 
            }
        }

        static int CountWords(string text)
        {
            int wordCount = text.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                           .Count();

            return wordCount;
        }
        static string ChooseTextFile(string directory)
        {
            string[] files = Directory.GetFiles(directory, "*.txt");
            if (files.Length == 0)
            {
                throw new Exception("No text files found in the specified directory!!");
            }

            string chosenFile = files[new Random().Next(files.Length)];
            return chosenFile;
        }

      

       
    }
}
