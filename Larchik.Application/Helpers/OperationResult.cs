namespace Larchik.Application.Helpers;

public class OperationResult<T>
{
    public bool IsSuccess { get; set; }
    public T Result { get; set; }
    public string Error { get; set; }

    public static OperationResult<T> Success(T value) => new OperationResult<T> {IsSuccess = true, Result = value};
    public static OperationResult<T> Failure(string error) => new OperationResult<T> {IsSuccess = false, Error = error}; 
}