﻿/* Yet Another Forum.NET
 * Copyright (C) 2003-2005 Bjørnar Henden
 * Copyright (C) 2006-2013 Jaben Cargman
 * Copyright (C) 2014-2023 Ingo Herbote
 * https://www.yetanotherforum.net/
 *
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at

 * https://www.apache.org/licenses/LICENSE-2.0

 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

namespace YAF.Core.Services;

using Microsoft.AspNetCore.Hosting;

using System;
using System.IO;

using YAF.Core.Model;
using YAF.Types.Models;

/// <summary>
///     The install upgrade service.
/// </summary>
public class InstallService : IHaveServiceLocator
{
    /// <summary>
    ///     The BBCode extensions import xml file.
    /// </summary>
    private const string BbcodeImport = "BBCodeExtensions.xml";

    /// <summary>
    ///     The Spam Words list import xml file.
    /// </summary>
    private const string SpamWordsImport = "SpamWords.xml";

    /// <summary>
    /// Initializes a new instance of the <see cref="InstallService"/> class.
    /// </summary>
    /// <param name="serviceLocator">
    /// The service locator.
    /// </param>
    /// <param name="raiseEvent">
    /// The raise Event.
    /// </param>
    /// <param name="access">
    /// The access.
    /// </param>
    public InstallService(IServiceLocator serviceLocator, IRaiseEvent raiseEvent, IDbAccess access)
    {
        this.RaiseEvent = raiseEvent;
        this.DbAccess = access;
        this.ServiceLocator = serviceLocator;
    }

    /// <summary>
    ///     Gets a value indicating whether this instance is forum installed.
    /// </summary>
    public bool IsForumInstalled
    {
        get
        {
            try
            {
                var boards = this.GetRepository<Board>().GetAll();
                return boards.Any();
            }
            catch
            {
                // failure... no boards.
                return false;
            }
        }
    }

    /// <summary>
    ///     Gets or sets the raise event.
    /// </summary>
    public IRaiseEvent RaiseEvent { get; set; }

    /// <summary>
    /// Gets or sets the database access.
    /// </summary>
    /// <value>
    /// The database access.
    /// </value>
    public IDbAccess DbAccess { get; set; }

    /// <summary>
    ///     Gets or sets the service locator.
    /// </summary>
    public IServiceLocator ServiceLocator { get; set; }

    /// <summary>
    /// Initializes the forum.
    /// </summary>
    /// <param name="applicationId">
    /// The application Id.
    /// </param>
    /// <param name="forumName">
    /// The forum name.
    /// </param>
    /// <param name="culture">
    /// The culture.
    /// </param>
    /// <param name="forumEmail">
    /// The forum email.
    /// </param>
    /// <param name="forumLogo">
    /// The forum Logo.
    /// </param>
    /// <param name="forumBaseUrlMask">
    /// The forum base URL mask.
    /// </param>
    /// <param name="adminUserName">
    /// The admin user name.
    /// </param>
    /// <param name="adminEmail">
    /// The admin email.
    /// </param>
    /// <param name="adminProviderUserKey">
    /// The admin provider user key.
    /// </param>
    public void InitializeForum(
        Guid applicationId,
        string forumName,
        string culture,
        string forumEmail,
        string forumLogo,
        string forumBaseUrlMask,
        string adminUserName,
        string adminEmail,
        string adminProviderUserKey)
    {
        var cult = StaticDataHelper.Cultures();

        var languageFromCulture = cult
            .FirstOrDefault(c => c.CultureTag == culture);

        var langFile = languageFromCulture != null ? languageFromCulture.CultureFile : "english.json";

        // -- initialize required 'registry' settings
        this.GetRepository<Registry>().Save("applicationid", applicationId.ToString());

        if (forumEmail.IsSet())
        {
            this.GetRepository<Registry>().Save("forumemail", forumEmail);
        }

        this.GetRepository<Registry>().Save("forumlogo", forumLogo);
        this.GetRepository<Registry>().Save("baseurlmask", forumBaseUrlMask);

        var boardId = this.GetRepository<Board>().Create(
            forumName,
            forumEmail,
            culture,
            langFile,
            adminUserName,
            adminEmail,
            adminProviderUserKey,
            true,
            string.Empty);

        // reload the board settings...
        BoardContext.Current.BoardSettings = this.Get<BoardSettingsService>().LoadBoardSettings(boardId, null);

        this.AddOrUpdateExtensions();
    }

    /// <summary>
    /// Tests database connection. Can probably be moved to DB class.
    /// </summary>
    /// <param name="exceptionMessage">
    /// The exception message.
    /// </param>
    /// <returns>
    /// The test database connection.
    /// </returns>
    public bool TestDatabaseConnection(out string exceptionMessage)
    {
        return this.DbAccess.TestConnection(out exceptionMessage);
    }

    /// <summary>
    /// Initialize Or Upgrade the Database
    /// </summary>
    /// <returns>
    /// The <see cref="bool"/>.
    /// </returns>
    public bool InitializeDatabase()
    {
        this.CreateTablesIfNotExists();

        this.ExecuteInstallScripts();

        this.GetRepository<Registry>().Save("version", this.Get<BoardInfo>().AppVersion.ToString());
        this.GetRepository<Registry>().Save("versionname", this.Get<BoardInfo>().AppVersionName);

        this.GetRepository<Registry>().Save("cdvversion", this.Get<BoardSettings>().CdvVersion++);

        return true;
    }

    /// <summary>
    /// Initializes the identity tables.
    /// </summary>
    public void InitializeIdentity()
    {
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<AspNetUsers>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<AspNetRoles>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<AspNetUserClaims>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<AspNetUserLogins>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<AspNetUserRoles>());
    }

    /// <summary>
    /// Executes the install scripts.
    /// </summary>
    private void ExecuteInstallScripts()
    {
        // Install Membership Scripts
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<AspNetUsers>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<AspNetRoles>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<AspNetUserClaims>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<AspNetUserLogins>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<AspNetUserRoles>());

        // Run other
        this.DbAccess.Execute(dbCommand => this.DbAccess.Information.CreateViews(this.DbAccess, dbCommand));

        this.DbAccess.Execute(dbCommand => this.DbAccess.Information.CreateIndexViews(this.DbAccess, dbCommand));
    }

    /// <summary>
    /// Create missing tables
    /// </summary>
    private void CreateTablesIfNotExists()
    {
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<Board>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<Rank>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<User>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<Category>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<Forum>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<Topic>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<Message>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<Thanks>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<Buddy>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<UserAlbum>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<UserAlbumImage>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<Active>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<ActiveAccess>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<Activity>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<AdminPageUserAccess>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<Group>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<BannedIP>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<BannedName>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<BannedEmail>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<BannedUserAgent>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<CheckEmail>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<Poll>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<Choice>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<PollVote>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<AccessMask>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<ForumAccess>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<MessageHistory>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<MessageReported>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<MessageReportedAudit>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<WatchForum>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<WatchTopic>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<Attachment>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<UserGroup>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<UserForum>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<NntpServer>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<NntpForum>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<NntpTopic>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<PrivateMessage>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<Replace_Words>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<Spam_Words>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<Registry>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<EventLog>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<BBCode>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<Medal>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<GroupMedal>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<UserMedal>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<IgnoreUser>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<TopicReadTracking>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<ForumReadTracking>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<ReputationVote>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<Tag>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<TopicTag>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<ProfileDefinition>());
        this.DbAccess.Execute(db => db.Connection.CreateTableIfNotExists<ProfileCustom>());
    }

    /// <summary>
    ///    Add or Update BBCode Extensions and Spam Words
    /// </summary>
    private void AddOrUpdateExtensions()
    {
        var loadWrapper = new Action<string, Action<Stream>>(
            (file, streamAction) =>
                {
                    var fullFile = Path.Combine(this.Get<IWebHostEnvironment>().WebRootPath, "Resources", file);

                    if (!File.Exists(fullFile))
                    {
                        return;
                    }

                    // import into board...
                    using var stream = new StreamReader(fullFile);
                    streamAction(stream.BaseStream);
                    stream.Close();
                });

        // get all boards...
        var boardIds = this.GetRepository<Board>().GetAll().Select(x => x.ID);

        // Upgrade all Boards
        boardIds.ForEach(
            boardId =>
                {
                    this.Get<IRaiseEvent>().Raise(new ImportStaticDataEvent(boardId));

                    // load default bbcode if available...
                    loadWrapper(BbcodeImport, s => this.Get<IDataImporter>().BBCodeExtensionImport(boardId, s));

                    // load default spam word if available...
                    loadWrapper(SpamWordsImport, s => this.Get<IDataImporter>().SpamWordsImport(boardId, s));
                });
    }
}