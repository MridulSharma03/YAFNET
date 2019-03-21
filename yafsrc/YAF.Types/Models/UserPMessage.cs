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
    using System.Data.Linq.Mapping;

    using ServiceStack.DataAnnotations;

    using YAF.Types.Interfaces.Data;

    /// <summary>
    /// A class which represents the yaf_UserPMessage table.
    /// </summary>
    [Serializable]
    [Table(Name = "UserPMessage")]
    public partial class UserPMessage : IEntity, IHaveID
    {
        partial void OnCreated();

        public UserPMessage()
        {
            this.OnCreated();
        }

        #region Properties


        [Alias("UserPMessageID")]
        [AutoIncrement]
        public int ID { get; set; }
        [References(typeof(User))]
        [Required]
        public int UserID { get; set; }
        [References(typeof(PMessage))]
        [Required]
        public int PMessageID { get; set; }
        [Required]
        public int Flags { get; set; }
        [Compute]
        public bool? IsRead { get; set; }
        [Compute]
        public bool? IsInOutbox { get; set; }
        [Compute]
        public bool? IsArchived { get; set; }
        [Compute]
        public bool? IsDeleted { get; set; }
        [Required]
        public bool IsReply { get; set; }

        #endregion
    }
}