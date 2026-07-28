namespace ChatBot.Api.Contracts;

public record StartSessionResponse(string SessionId);

public record ChatMessageRequest(string Message);

public record ChatMessageResponse(string SessionId, string Reply);
