﻿using System.Text.Json.Serialization;

namespace AIPoweredSQLConverter.API
{
    public class ChatProfile
    {
        [JsonIgnore]
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Model { get; set; }
        public string? RagDatabase { get; set; }
        public float? Frequency_Penalty { get; set; }
        public float? Presence_Penalty { get; set; }
        public float? Temperature { get; set; }
        public float? Top_P { get; set; }
        public int? Max_Tokens { get; set; }
        public int? Top_Logprobs { get; set; }
        public bool? Logprobs { get; set; }
        public string? User { get; set; }
        public string? Tool_Choice { get; set; }
        public string? Response_Format { get; set; }
        public string? System_Message { get; set; }
        public string[]? Stop { get; set; }
        public int? MaxMessageHistory { get; set; }
        public bool? Return_Recursion { get; set; }
        public string[]? Reference_Profiles { get; set; }
    }
}
