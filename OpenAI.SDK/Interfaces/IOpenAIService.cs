namespace OpenAI.Interfaces;

public interface IOpenAIService
{
    /// <summary>
    ///     Files are used to upload documents that can be used across features like <see cref="FineTunes" />
    /// </summary>
    public IFileService Files { get; }

    /// <summary>
    ///     Beta
    /// </summary>
    public IBetaService Beta { get; }
}