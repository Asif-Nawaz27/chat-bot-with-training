namespace ChatBot.Data.Chat;

/// <summary>Runs the interactive console chat loop against a trained model.</summary>
public interface IChatService
{
    void Run(GptConfig config);
}
