using System.Text;
using FellowOakDicom;
using FellowOakDicom.Network;
using Microsoft.Extensions.Logging;

public class ScpOptions
{
    public string StorageFolder { get; set; } = "dicom_storage";
    public HashSet<string> AllowedCallingAETitles { get; set; } = new() { "*" }; // "*" = accept all
    public bool AllowAllCallingAE { get; set; } = true;
    public bool PreventDuplicates { get; set; } = true;
}

public class SCPService : DicomService, IDicomServiceProvider, IDicomCStoreProvider, IDicomCEchoProvider, IDicomCFindProvider, IDicomCMoveProvider, IDicomCGetProvider
{
    private readonly ILogger _logger;
    private readonly string _storagePath = "dicom_storage";
    // Danh sách SOP Class được cho phép
    private readonly DicomUID[] allowedSopClasses = new[]
    {
        // C-ECHO
        DicomUID.Verification,

        // C-STORE (bạn có thể thêm nhiều loại image)
        DicomUID.SecondaryCaptureImageStorage,
        DicomUID.UltrasoundImageStorage,

        // C-FIND
        DicomUID.PatientRootQueryRetrieveInformationModelFind,
        DicomUID.StudyRootQueryRetrieveInformationModelFind,
        DicomUID.PatientStudyOnlyQueryRetrieveInformationModelFindRETIRED,

        // Worklist (Modality Worklist)
        DicomUID.ModalityWorklistInformationModelFind
    };
    // Transfer Syntax mình hỗ trợ
    private readonly DicomTransferSyntax[] AcceptedTransferSyntaxes =
    {
        DicomTransferSyntax.ExplicitVRLittleEndian,
        DicomTransferSyntax.ImplicitVRLittleEndian,
        DicomTransferSyntax.JPEGLSLossless,
        DicomTransferSyntax.JPEG2000Lossless,
        DicomTransferSyntax.JPEG2000Lossy,
        DicomTransferSyntax.RLELossless
    };

    public SCPService(INetworkStream stream, Encoding fallbackEncoding, ILogger logger, DicomServiceDependencies dependencies)
    : base(stream, fallbackEncoding, logger, dependencies)
    {
        _logger = logger;
    }

    public Task OnReceiveAssociationRequestAsync(DicomAssociation association)
    {
        
        foreach (var pc in association.PresentationContexts)
            pc.SetResult(DicomPresentationContextResult.Accept);
                
        _logger.LogInformation("🔗 Association from AE: {AE}", association.CallingAE);
        return SendAssociationAcceptAsync(association);
    }

    public Task<DicomCEchoResponse> OnCEchoRequestAsync(DicomCEchoRequest request)
    {
        _logger.LogInformation("📟 Received C-ECHO from AE: {AE}", Association.CallingAE);
        return Task.FromResult(new DicomCEchoResponse(request, DicomStatus.Success));
    }

    public async Task<DicomCStoreResponse> OnCStoreRequestAsync(DicomCStoreRequest request)
    {
        Directory.CreateDirectory(_storagePath);
        var filePath = Path.Combine(_storagePath, request.SOPInstanceUID.UID + ".dcm");        
        await request.File.SaveAsync(filePath);

        _logger.LogInformation("📥 Saved DICOM file: {File}", filePath);
        return new DicomCStoreResponse(request, DicomStatus.Success);
    }

    public Task OnCStoreRequestExceptionAsync(string tempFileName, Exception e)
        => Task.CompletedTask;

    public Task OnReceiveAssociationReleaseRequestAsync()
        => Task.CompletedTask;

    public void OnReceiveAbort(DicomAbortSource source, DicomAbortReason reason) { }

    public void OnConnectionClosed(Exception? exception) { }

    public async IAsyncEnumerable<DicomCFindResponse> OnCFindRequestAsync(DicomCFindRequest request)
    {
        _logger.LogInformation("📋 Received C-FIND (Worklist) from AE: {AE}", Association.CallingAE);

        // Chỉ xử lý Worklist Query
        if (request.SOPClassUID != DicomUID.ModalityWorklistInformationModelFind)
        {
            yield return new DicomCFindResponse(request, DicomStatus.QueryRetrieveUnableToProcess);
            yield break;
        }

        // ---- Tạo dataset Worklist Demo ----
        var demoDataset = new DicomDataset
        {
            { DicomTag.SpecificCharacterSet, "ISO_IR 100" },

            { DicomTag.PatientName, "NGUYEN^VAN A" },
            { DicomTag.PatientID, "BN001" },
            { DicomTag.PatientBirthDate, "19800101" },
            { DicomTag.PatientSex, "M" },

            { DicomTag.AccessionNumber, "ACC20250101" },
            { DicomTag.RequestedProcedureDescription, "Ultrasound Abdomen" },
            { DicomTag.RequestedProcedureID, "RP1001" },
            { DicomTag.StudyInstanceUID, DicomUID.Generate() },
        };

        // Sequence: Scheduled Procedure Step
        var sps = new DicomDataset
        {
            { DicomTag.ScheduledStationAETitle, "USSERVER" },
            { DicomTag.Modality, "US" },
            { DicomTag.ScheduledProcedureStepStartDate, "20250101" },
            { DicomTag.ScheduledProcedureStepStartTime, "080000" },
            { DicomTag.ScheduledPerformingPhysicianName, "DR^TRAN" },
            { DicomTag.ScheduledProcedureStepDescription, "Abdomen Ultrasound" },
            { DicomTag.ScheduledProcedureStepID, "SPS1001" }
        };

        demoDataset.Add(new DicomSequence(DicomTag.ScheduledProcedureStepSequence, sps));

        // ----- TRẢ WORKLIST ENTRY -----
        var pendingResponse = new DicomCFindResponse(request, DicomStatus.Pending);
        pendingResponse.Dataset = demoDataset;     // GÁN DỮ LIỆU WORKLIST
        yield return pendingResponse;

        // ----- KẾT THÚC QUERY -----
        var finalResponse = new DicomCFindResponse(request, DicomStatus.Success);
        yield return finalResponse;
    }


    public IAsyncEnumerable<DicomCMoveResponse> OnCMoveRequestAsync(DicomCMoveRequest request)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<DicomCGetResponse> OnCGetRequestAsync(DicomCGetRequest request)
    {
        throw new NotImplementedException();
    }
}
