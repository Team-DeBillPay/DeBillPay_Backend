namespace DeBillPay_Backend.DTOs {
    public class UpdateEditorRightsDto
    {
        public List<ParticipantEditorRights> Participants { get; set; }
    }

    public class ParticipantEditorRights
    {
        public int ParticipantId { get; set; }
        public bool IsEditorRights { get; set; }
    }
}