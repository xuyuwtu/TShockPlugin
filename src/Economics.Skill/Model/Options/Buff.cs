﻿using Newtonsoft.Json;

namespace Economics.Skill.Model.Options;

public class Buff
{
    [JsonProperty("BuffID")]
    public int BuffId { get; set; }

    [JsonProperty("时长")]
    public int Time { get; set; }
}