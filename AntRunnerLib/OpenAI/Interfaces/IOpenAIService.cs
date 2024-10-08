﻿namespace OpenAI.Interfaces;

public interface IOpenAiService
{
    /// <summary>
    /// Files are used to upload documents that can be used across features like FineTunes />
    /// </summary>
    public IFileService Files { get; }

    /// <summary>
    /// Beta
    /// </summary>
    public IBetaService Beta { get; }
}