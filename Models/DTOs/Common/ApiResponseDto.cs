namespace FloreriaBautista.Models.DTOs.Common;

public class ApiResponseDto<T>
{
    public bool   Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;
    public T?     Data    { get; set; }

    public static ApiResponseDto<T> Ok(T data, string message = "")
        => new() { Data = data, Message = message };

    public static ApiResponseDto<T> Fail(string message)
        => new() { Success = false, Message = message };
}
