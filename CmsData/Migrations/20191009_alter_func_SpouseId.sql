GO
IF OBJECT_ID('[dbo].[SpouseId]') IS NOT NULL
DROP FUNCTION [dbo].[SpouseId] 
GO
CREATE FUNCTION [dbo].[SpouseId] ( @peopleid INT )
RETURNS int
AS
BEGIN
	DECLARE @Result int

	SELECT TOP 1 @Result = s.PeopleId FROM dbo.People p
	JOIN dbo.People s ON s.FamilyId = p.FamilyId
	JOIN lookup.MaritalStatus ms ON ms.id = p.MaritalStatusId
	JOIN lookup.FamilyPosition fp ON fp.id = p.PositionInFamilyId
	WHERE s.PeopleId <> @peopleid AND p.PeopleId = @peopleid
	AND p.MaritalStatusId = s.MaritalStatusId
	AND ms.Married = 1
	AND s.DeceasedDate IS NULL
	AND p.DeceasedDate IS NULL
	AND p.PositionInFamilyId = s.PositionInFamilyId
	and fp.PrimaryAdult = 1
	AND s.FirstName <> 'Duplicate'	-- Return the result of the function
	
	RETURN @Result

END
