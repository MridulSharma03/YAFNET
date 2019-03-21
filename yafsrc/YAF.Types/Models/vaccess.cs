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

    using ServiceStack.DataAnnotations;

    using YAF.Types.Interfaces.Data;

    /// <summary>
    ///     A class which represents the yaf_vaccess views.
    /// </summary>
    [Serializable]
    public partial class vaccess : IEntity
    {
        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="vaccess"/> class.
        /// </summary>
        public vaccess()
        {
            this.OnCreated();
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets or sets the user id.
        /// </summary>
        [Required]
        public int UserID { get; set; }

        /// <summary>
        /// Gets or sets the forum id.
        /// </summary>
        public int ForumID { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether is admin.
        /// </summary>
        public bool IsAdmin { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether is forum moderator.
        /// </summary>
        public bool IsForumModerator { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether is moderator.
        /// </summary>
        public bool IsModerator { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether read access.
        /// </summary>
        public bool ReadAccess { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether post access.
        /// </summary>
        public bool PostAccess { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether reply access.
        /// </summary>
        public bool ReplyAccess { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether priority access.
        /// </summary>
        public bool PriorityAccess { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether poll access.
        /// </summary>
        public bool PollAccess { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether vote access.
        /// </summary>
        public bool VoteAccess { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether moderator access.
        /// </summary>
        public bool ModeratorAccess { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether edit access.
        /// </summary>
        public bool EditAccess { get; set; }
        

        /// <summary>
        /// Gets or sets a value indicating whether delete access.
        /// </summary>
        public bool DeleteAccess { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether upload access.
        /// </summary>
        public bool UploadAccess { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether download access.
        /// </summary>
        public bool DownloadAccess { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// The on created.
        /// </summary>
        partial void OnCreated();

        #endregion
    }
}