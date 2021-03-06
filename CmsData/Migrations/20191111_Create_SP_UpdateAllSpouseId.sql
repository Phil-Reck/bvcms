DROP PROCEDURE IF EXISTS [dbo].[UpdateAllSpouseId]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE PROCEDURE [dbo].[UpdateAllSpouseId]
AS
BEGIN
	SET NOCOUNT ON;

	UPDATE Families
	SET HeadOfHouseholdSpouseId = dbo.SpouseId(HeadOfHouseholdId)
	WHERE HeadOfHouseholdSpouseId IS NULL
	
	UPDATE People
	SET SpouseId = dbo.SpouseId(PeopleId)
	WHERE SpouseId IS NULL
END

