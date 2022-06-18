using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ToDoList
{
    public class Model
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Creator { get; set; }
        public string CreateDate { get; set; }
        public string EndDate { get; set; }
        public string Status { get; set; } = "new";
        public string Description { get; set; }
        [JsonIgnore]
        public int TakenId { get; set; }
        [JsonIgnore]
        public List<Model> ListOfToDo { get; set; }
    }
}