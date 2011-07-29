﻿SET NUMERIC_ROUNDABORT OFF
GO
SET ANSI_PADDING, ANSI_WARNINGS, CONCAT_NULL_YIELDS_NULL, ARITHABORT, QUOTED_IDENTIFIER, ANSI_NULLS ON
GO
IF EXISTS (SELECT * FROM tempdb..sysobjects WHERE id=OBJECT_ID('tempdb..#tmpErrors')) DROP TABLE #tmpErrors
GO
CREATE TABLE #tmpErrors (Error int)
GO
SET XACT_ABORT ON
GO
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE
GO
BEGIN TRANSACTION
GO
PRINT N'Creating schemata'
GO
PRINT N'Creating [dbo].[InsertMessage]'
GO
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[InsertMessage](@msg VARCHAR(200))
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;
	
	DECLARE @dialog UNIQUEIDENTIFIER
	DECLARE @message NVARCHAR(50)
	BEGIN DIALOG CONVERSATION @dialog
	FROM SERVICE EmailSendService
	TO SERVICE 'EmailReceiveService'
	ON CONTRACT EmailContract
	WITH Encryption = OFF;
	
	SEND ON CONVERSATION @dialog
	MESSAGE TYPE EmailRequest (@msg)
	
END

GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO
PRINT N'Creating [dbo].[QueueScheduledEmails]'
GO
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[QueueScheduledEmails]
AS
BEGIN

DECLARE @Id INT
DECLARE @Host VARCHAR(50)
DECLARE @CmsHost VARCHAR(50)
DECLARE @msg VARCHAR(200)

DECLARE c1 CURSOR READ_ONLY
FOR
SELECT Id, Host, CmsHost FROM BlogData.dbo.ScheduledEmailAll

OPEN c1

FETCH NEXT FROM c1 INTO @Id, @Host, @CmsHost

WHILE @@FETCH_STATUS = 0
BEGIN

	
	SET @msg = CONVERT(VARCHAR(10), @id) + '|' + @CmsHost + '|' + @Host
	EXEC dbo.InsertMessage @msg

	FETCH NEXT FROM c1
	INTO @Id, @Host, @CmsHost

END

CLOSE c1
DEALLOCATE c1

END

GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO
PRINT N'Creating [dbo].[SelectFromQueue]'
GO

CREATE VIEW [dbo].[SelectFromQueue]
	AS
   SELECT CONVERT(VARCHAR(max), message_body) AS message FROM EmailReceiveQueue
GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO
PRINT N'Creating [dbo].[GetTargetHandle]'
GO

CREATE FUNCTION [dbo].[GetTargetHandle] ( @conversationID AS UNIQUEIDENTIFIER )
RETURNS UNIQUEIDENTIFIER
AS
	BEGIN
		DECLARE @conversationHandle AS UNIQUEIDENTIFIER
		
		SELECT @conversationHandle = conversation_handle
		FROM sys.conversation_endpoints
		WHERE conversation_id = @conversationID
		AND is_initiator=0
		
		RETURN @conversationHandle
	END


GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO
PRINT N'Creating [dbo].[GetInitiatorHandle]'
GO
CREATE FUNCTION [dbo].[GetInitiatorHandle] ( @conversationID AS UNIQUEIDENTIFIER )
RETURNS UNIQUEIDENTIFIER
AS
	BEGIN
		DECLARE @conversationHandle AS UNIQUEIDENTIFIER
		
		SELECT @conversationHandle = conversation_handle
		FROM sys.conversation_endpoints
		WHERE conversation_id = @conversationID
		AND is_initiator=1
		
		RETURN @conversationHandle
	END
	

GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO
PRINT N'Creating [dbo].[SendMessage]'
GO
CREATE PROCEDURE [dbo].[SendMessage](@conversation UNIQUEIDENTIFIER, @msg VARCHAR(200))
AS
BEGIN
	DECLARE @h UNIQUEIDENTIFIER
	SELECT @h = dbo.GetInitiatorHandle(@conversation);
	SEND ON CONVERSATION @h 
	MESSAGE TYPE [DEFAULT] (@msg)
END
GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO
PRINT N'Creating [dbo].[EndConversation]'
GO

CREATE PROCEDURE [dbo].[EndConversation] ( @conversationID AS UNIQUEIDENTIFIER )
AS
BEGIN
	DECLARE @initiatorHandle AS UNIQUEIDENTIFIER
	DECLARE @targetHandle AS UNIQUEIDENTIFIER
	
	SET @initiatorHandle = dbo.GetInitiatorHandle(@conversationID)
	SET @targetHandle = dbo.GetTargetHandle(@conversationID)
	
	IF @initiatorHandle IS NOT NULL
	BEGIN
		END CONVERSATION @initiatorHandle WITH CLEANUP
		PRINT 'ended Initiator'
	END
	
	IF @targetHandle IS NOT NULL
	BEGIN
		END CONVERSATION @targetHandle WITH CLEANUP
		PRINT 'ended Target'
	END

	RETURN
END

GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO
PRINT N'Creating [dbo].[Databases]'
GO


CREATE VIEW [dbo].[Databases]
	AS
	SELECT name
	FROM sys.databases
	WHERE name LIKE 'CMS[_]%' AND name NOT LIKE 'CMS[_]%[_]img'
	AND name <> 'CMS_test'


GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO
PRINT N'Creating [dbo].[ScheduledEmailAll]'
GO

CREATE VIEW [dbo].[ScheduledEmailAll]
	AS
	SELECT Id, 'abchurch' Host, (SELECT Setting FROM [CMS_abchurch].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_abchurch].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'alliancechurch' Host, (SELECT Setting FROM [CMS_alliancechurch].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_alliancechurch].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'ambassadorchurch' Host, (SELECT Setting FROM [CMS_ambassadorchurch].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_ambassadorchurch].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'apostolicfaith' Host, (SELECT Setting FROM [CMS_apostolicfaith].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_apostolicfaith].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'auburncornerstone' Host, (SELECT Setting FROM [CMS_auburncornerstone].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_auburncornerstone].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'bellavista' Host, (SELECT Setting FROM [CMS_bellavista].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_bellavista].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'cccnc' Host, (SELECT Setting FROM [CMS_cccnc].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_cccnc].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'cccpenndel' Host, (SELECT Setting FROM [CMS_cccpenndel].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_cccpenndel].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'ccjasper2' Host, (SELECT Setting FROM [CMS_ccjasper2].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_ccjasper2].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'cfmc' Host, (SELECT Setting FROM [CMS_cfmc].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_cfmc].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'churchatriverhills' Host, (SELECT Setting FROM [CMS_churchatriverhills].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_churchatriverhills].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'citywideredeemer' Host, (SELECT Setting FROM [CMS_citywideredeemer].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_citywideredeemer].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'connectingpoint' Host, (SELECT Setting FROM [CMS_connectingpoint].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_connectingpoint].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'coptic-zambia' Host, (SELECT Setting FROM [CMS_coptic-zambia].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_coptic-zambia].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'cornerstonebible' Host, (SELECT Setting FROM [CMS_cornerstonebible].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_cornerstonebible].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'Cottonwood' Host, (SELECT Setting FROM [CMS_Cottonwood].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_Cottonwood].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'crossconnectioninfo' Host, (SELECT Setting FROM [CMS_crossconnectioninfo].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_crossconnectioninfo].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'crossroadsofjoliet' Host, (SELECT Setting FROM [CMS_crossroadsofjoliet].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_crossroadsofjoliet].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'demo' Host, (SELECT Setting FROM [CMS_demo].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_demo].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'edgechurch' Host, (SELECT Setting FROM [CMS_edgechurch].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_edgechurch].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'efcnewlife' Host, (SELECT Setting FROM [CMS_efcnewlife].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_efcnewlife].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'elevate' Host, (SELECT Setting FROM [CMS_elevate].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_elevate].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'epicmishawaka' Host, (SELECT Setting FROM [CMS_epicmishawaka].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_epicmishawaka].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'faithbaptistwamo' Host, (SELECT Setting FROM [CMS_faithbaptistwamo].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_faithbaptistwamo].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'familyfellowshipchurch' Host, (SELECT Setting FROM [CMS_familyfellowshipchurch].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_familyfellowshipchurch].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'fbcchalmette' Host, (SELECT Setting FROM [CMS_fbcchalmette].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_fbcchalmette].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'fbcgalax' Host, (SELECT Setting FROM [CMS_fbcgalax].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_fbcgalax].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'fbcgoodlettsville' Host, (SELECT Setting FROM [CMS_fbcgoodlettsville].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_fbcgoodlettsville].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'fcbcwalnut' Host, (SELECT Setting FROM [CMS_fcbcwalnut].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_fcbcwalnut].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'firstbornministries' Host, (SELECT Setting FROM [CMS_firstbornministries].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_firstbornministries].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'firstfwb' Host, (SELECT Setting FROM [CMS_firstfwb].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_firstfwb].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'freedomchurch' Host, (SELECT Setting FROM [CMS_freedomchurch].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_freedomchurch].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'gracecommunitychurch' Host, (SELECT Setting FROM [CMS_gracecommunitychurch].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_gracecommunitychurch].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'gracelouisville' Host, (SELECT Setting FROM [CMS_gracelouisville].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_gracelouisville].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'gracepointe' Host, (SELECT Setting FROM [CMS_gracepointe].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_gracepointe].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'gracepointebc' Host, (SELECT Setting FROM [CMS_gracepointebc].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_gracepointebc].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'hintonavenueumc' Host, (SELECT Setting FROM [CMS_hintonavenueumc].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_hintonavenueumc].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'hope' Host, (SELECT Setting FROM [CMS_hope].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_hope].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'hope-cc' Host, (SELECT Setting FROM [CMS_hope-cc].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_hope-cc].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'hopecommunity' Host, (SELECT Setting FROM [CMS_hopecommunity].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_hopecommunity].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'hopevalleychurch' Host, (SELECT Setting FROM [CMS_hopevalleychurch].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_hopevalleychurch].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'jcboston' Host, (SELECT Setting FROM [CMS_jcboston].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_jcboston].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'jessr' Host, (SELECT Setting FROM [CMS_jessr].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_jessr].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'lifepoint365' Host, (SELECT Setting FROM [CMS_lifepoint365].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_lifepoint365].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'liveoak' Host, (SELECT Setting FROM [CMS_liveoak].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_liveoak].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'msm' Host, (SELECT Setting FROM [CMS_msm].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_msm].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'mytruelife' Host, (SELECT Setting FROM [CMS_mytruelife].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_mytruelife].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'pointharbor' Host, (SELECT Setting FROM [CMS_pointharbor].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_pointharbor].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'pointharbor2' Host, (SELECT Setting FROM [CMS_pointharbor2].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_pointharbor2].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'providencehill' Host, (SELECT Setting FROM [CMS_providencehill].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_providencehill].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'redeemer' Host, (SELECT Setting FROM [CMS_redeemer].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_redeemer].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'ridgepointchurch' Host, (SELECT Setting FROM [CMS_ridgepointchurch].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_ridgepointchurch].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'saucierbaptistchurch' Host, (SELECT Setting FROM [CMS_saucierbaptistchurch].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_saucierbaptistchurch].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'silverdale' Host, (SELECT Setting FROM [CMS_silverdale].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_silverdale].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'starter' Host, (SELECT Setting FROM [CMS_starter].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_starter].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'StarterDb' Host, (SELECT Setting FROM [CMS_StarterDb].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_StarterDb].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'stmaryscathedral' Host, (SELECT Setting FROM [CMS_stmaryscathedral].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_stmaryscathedral].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'thegatheringnj' Host, (SELECT Setting FROM [CMS_thegatheringnj].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_thegatheringnj].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'thegrove' Host, (SELECT Setting FROM [CMS_thegrove].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_thegrove].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'thegrovelife' Host, (SELECT Setting FROM [CMS_thegrovelife].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_thegrovelife].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'trinitycolumbia' Host, (SELECT Setting FROM [CMS_trinitycolumbia].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_trinitycolumbia].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'westphalbaptist' Host, (SELECT Setting FROM [CMS_westphalbaptist].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_westphalbaptist].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'whillschurch' Host, (SELECT Setting FROM [CMS_whillschurch].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_whillschurch].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'wlafaith' Host, (SELECT Setting FROM [CMS_wlafaith].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_wlafaith].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
UNION ALL SELECT Id, 'worshiplife' Host, (SELECT Setting FROM [CMS_worshiplife].dbo.Setting WHERE Id = 'DefaultHost') CmsHost FROM [CMS_worshiplife].dbo.EmailQueue WHERE SendWhen IS NOT NULL AND Sent IS NULL AND DATEADD(Day, 0, DATEDIFF(Day, 0, SendWhen)) = DATEADD(Day, 0, DATEDIFF(Day, 0, GetDate()))
GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO
PRINT N'Creating [dbo].[CleanUpAllConversations]'
GO

CREATE PROCEDURE [dbo].[CleanUpAllConversations]
AS
	WHILE EXISTS(SELECT * FROM sys.conversation_endpoints)
	BEGIN
		DECLARE @conversationHandle AS UNIQUEIDENTIFIER

		SELECT TOP(1) @conversationHandle=conversation_handle FROM sys.conversation_endpoints

		END CONVERSATION @conversationHandle WITH CLEANUP
	END
	RETURN

GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO
PRINT N'Creating [dbo].[CreateNewConversation]'
GO

CREATE PROCEDURE [dbo].[CreateNewConversation] ( @conversationID AS UNIQUEIDENTIFIER OUTPUT )
AS
BEGIN
	DECLARE @dialogHandle AS UNIQUEIDENTIFIER

	--	Create a dialog
	BEGIN DIALOG CONVERSATION @dialogHandle
	FROM SERVICE [EmailService]
	TO SERVICE 'EmailService'
	ON CONTRACT NormalEmailContract
	WITH
	ENCRYPTION = OFF
	
	SELECT @conversationId = conversation_id
	FROM sys.conversation_endpoints
	WHERE conversation_handle = @dialogHandle

END

GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO
PRINT N'Creating [dbo].[CreateNewPriorityConversation]'
GO

CREATE PROCEDURE [dbo].[CreateNewPriorityConversation] ( @conversationID AS UNIQUEIDENTIFIER OUTPUT )
AS
BEGIN
	DECLARE @dialogHandle AS UNIQUEIDENTIFIER

	--	Create a dialog
	BEGIN DIALOG CONVERSATION @dialogHandle
	FROM SERVICE EmailService
	TO SERVICE 'EmailService'
	ON CONTRACT PriorityEmailContract
	WITH
	ENCRYPTION = OFF
	
	SELECT @conversationId = conversation_id
	FROM sys.conversation_endpoints
	WHERE conversation_handle = @dialogHandle
END

GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO
PRINT N'Creating [dbo].[ShowConversations]'
GO
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[ShowConversations]
	-- Add the parameters for the stored procedure here
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT * FROM sys.conversation_endpoints
END
GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO
PRINT N'Creating message types'
GO
CREATE MESSAGE TYPE [EmailRequest]
AUTHORIZATION [dbo]
VALIDATION=NONE
GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO
PRINT N'Creating contracts'
GO
CREATE CONTRACT [EmailContract]
AUTHORIZATION [dbo] ( 
[EmailRequest] SENT BY INITIATOR
)
GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO
CREATE CONTRACT [NormalEmailContract]
AUTHORIZATION [dbo] ( 
[DEFAULT] SENT BY INITIATOR
)
GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO
CREATE CONTRACT [PriorityEmailContract]
AUTHORIZATION [dbo] ( 
[DEFAULT] SENT BY INITIATOR
)
GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO
PRINT N'Creating queues'
GO
CREATE QUEUE [dbo].[EmailSendQueue] 
WITH STATUS=ON, 
RETENTION=OFF
GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO
CREATE QUEUE [dbo].[EmailReceiveQueue] 
WITH STATUS=ON, 
RETENTION=OFF
GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO
CREATE QUEUE [dbo].[EmailQueue] 
WITH STATUS=ON, 
RETENTION=OFF
GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO
PRINT N'Creating services'
GO
CREATE SERVICE [EmailReceiveService]
AUTHORIZATION [dbo]
ON QUEUE [dbo].[EmailReceiveQueue]
(
[EmailContract]
)
GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO
CREATE SERVICE [EmailSendService]
AUTHORIZATION [dbo]
ON QUEUE [dbo].[EmailSendQueue]
(
[EmailContract]
)
GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO
CREATE SERVICE [EmailService]
AUTHORIZATION [dbo]
ON QUEUE [dbo].[EmailQueue]
(
[NormalEmailContract],
[PriorityEmailContract]
)
GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO
IF EXISTS (SELECT * FROM #tmpErrors) ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT>0 BEGIN
PRINT 'The database update succeeded'
COMMIT TRANSACTION
END
ELSE PRINT 'The database update failed'
GO
DROP TABLE #tmpErrors
GO
SET NUMERIC_ROUNDABORT OFF
GO
SET ANSI_PADDING, ANSI_WARNINGS, CONCAT_NULL_YIELDS_NULL, ARITHABORT, QUOTED_IDENTIFIER, ANSI_NULLS, NOCOUNT ON
GO
SET DATEFORMAT YMD
GO
SET XACT_ABORT ON
GO
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE
GO
BEGIN TRANSACTION
-- Pointer used for text / image updates. This might not be needed, but is declared here just in case
DECLARE @pv binary(16)
COMMIT TRANSACTION
GO
