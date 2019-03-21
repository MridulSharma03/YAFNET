/* Yet Another Forum.NET
 * Copyright (C) 2003-2005 Bjørnar Henden
 * Copyright (C) 2006-2013 Jaben Cargman
 * Copyright (C) 2014-2019 Ingo Herbote
 * http://www.yetanotherforum.net/
 * 
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at

 * http://www.apache.org/licenses/LICENSE-2.0

 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */
namespace YAF.Types.Models
{
    using System;
    using System.Data;
    using System.Data.Linq.Mapping;

    using ServiceStack.DataAnnotations;

    using YAF.Types.Flags;
    using YAF.Types.Interfaces.Data;

    /// <summary>
    /// A class which represents the Message table.
    /// </summary>
    [Serializable]
    [Table(Name = "Message")]
    public partial class Message : IEntity, IHaveID
    {
        partial void OnCreated();

        public Message()
        {
            this.OnCreated();
        }

        public Message([NotNull] DataRow row)
        {
            this.ID = row.Field<int?>("MessageID") ?? 0;
            this.UserID = row.Field<int?>("UserID") ?? 0;
            this.UserName = row.Field<string>("UserName");
            this.MessageText = row.Field<string>("Message");
            this.TopicID = row.Field<int?>("TopicID") ?? 0;

            this.Posted = row.Field<DateTime?>("Posted").Value;

            try
            {
                this.Topic = row.Field<string>("Topic");
            }
            catch (ArgumentException)
            {
                this.Topic = row.Field<string>("Subject");
            }

            this.Flags = row.Field<int?>("Flags") ?? 0;

            if (row.Table.Columns.Contains("Edited"))
            {
                this.Edited = row.Field<DateTime?>("Edited");
                this.EditReason = row.Field<string>("EditReason");
            }

            try
            {
                this.Position = row.Field<int?>("Position") ?? 0;
            }
            catch (Exception)
            {
                this.Position = 0;
            }


            try
            {
                this.IsModeratorChanged = row.Field<bool?>("IsModeratorChanged");
            }
            catch (Exception)
            {
                this.IsModeratorChanged = false;
            }
            
            try
            {
                this.DeleteReason = row.Field<string>("DeleteReason");
            }
            catch (Exception)
            {
                this.DeleteReason = string.Empty;
            }
            
            try
            {
                this.BlogPostID = row.Field<string>("BlogPostID");
            }
            catch (Exception)
            {
                this.BlogPostID = string.Empty;
            }
            
            try
            {
                this.IP = row.Field<string>("IP");
            }
            catch (Exception)
            {
                this.IP = string.Empty;
            }
           
            try
            {
                this.ExternalMessageId = row.Field<string>("ExternalMessageId");
            }
            catch (Exception)
            {
                this.ExternalMessageId = string.Empty;
            }
            
            try
            {
                this.ReferenceMessageId = row.Field<string>("ReferenceMessageId");
            }
            catch (Exception)
            {
                this.ReferenceMessageId = string.Empty;
            }
            
            try
            {
                this.HasAttachments = row.Field<bool?>("HasAttachments");
            }
            catch (Exception)
            {
                this.HasAttachments = false;
            }
        }

        #region Properties

        [AutoIncrement]
        [AliasAttribute("MessageID")]
        public int ID { get; set; }

        [Ignore]
        public string Topic { get; set; }

        [References(typeof(Topic))]
        [Required]
        public int TopicID { get; set; }
        [References(typeof(Message))]
        public int? ReplyTo { get; set; }
        [Required]
        public int Position { get; set; }
        [Required]
        public int Indent { get; set; }
        [References(typeof(User))]
        [Required]
        public int UserID { get; set; }
        public string UserName { get; set; }

        [Ignore]
        public bool? HasAttachments { get; set; }
        [Required]
        public DateTime Posted { get; set; }
        [Alias("Message")]
        public string MessageText { get; set; }
        [Required]
        public string IP { get; set; }
        public DateTime? Edited { get; set; }
        [Required]
        public int Flags { get; set; }

        [Ignore]
        public MessageFlags MessageFlags
        {
            get
            {
                return new MessageFlags(this.Flags);
            }

            set
            {
                this.Flags = value.BitValue;
            }
        }
        public string EditReason { get; set; }

        [Ignore]
        public string Signature { get; set; }
        [Required]
        public bool? IsModeratorChanged { get; set; }
        public string DeleteReason { get; set; }
        [Compute]
        public bool? IsDeleted { get; set; }
        [Compute]
        public bool? IsApproved { get; set; }
        public string BlogPostID { get; set; }
        public int? EditedBy { get; set; }
        public string ExternalMessageId { get; set; }
        public string ReferenceMessageId { get; set; }
        public string UserDisplayName { get; set; }

        #endregion
    }
}