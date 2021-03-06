IF EXISTS (
    SELECT * FROM sysobjects WHERE id = object_id(N'IsValidEmail') 
    AND xtype IN (N'FN', N'FS')
)
    DROP FUNCTION IsValidEmail
GO
CREATE FUNCTION [dbo].[IsValidEmail](@Str nvarchar(500))
RETURNS BIT
AS
BEGIN
        DECLARE @isValid bit
        DECLARE @domain nvarchar(500)
		SET @isValid = 1
		SET @Str = LTRIM(RTRIM(@Str))

		IF @Str = ''
		OR @Str IS NULL
			BEGIN
				RETURN @isValid
			END

        IF 
		PATINDEX ('%[ ,":;\()<>]%',@Str) > 0 -- Invalid characters
		OR PATINDEX ('%[[]%',@Str) > 0 -- Invalid character
		OR PATINDEX ('%]%',@Str) > 0 -- Invalid character
		OR PATINDEX ('[@._]%', @Str) > 0 -- Valid but cannot be starting character
		OR PATINDEX ('-%', @Str) > 0 -- Valid but cannot be starting character
		OR PATINDEX ('%[@._]', @Str) > 0 -- Valid but cannot be ending character
		OR PATINDEX ('%-', @Str) > 0 -- Valid but cannot be ending character
		OR @Str NOT LIKE '%@%.%' -- Must contain at least one @ and one .
		OR @Str LIKE '%..%' -- Cannot have two periods in a row
		OR @Str LIKE '%@%@%' -- Cannot have two @ anywhere
		OR @Str LIKE '%.@%' OR @Str LIKE '%@.%' -- Cannot have @ and . next to each other
            BEGIN
                SET @isValid = 0
            END        

		SET @domain = SUBSTRING(@Str, CHARINDEX('@', @Str)+1, LEN(@Str)-CHARINDEX('@', @Str))

		IF
		PATINDEX ('%[!#$%&*+/=?^_`{|}~]%', @domain) > 0 -- Valid but cannot be starting character
		OR PATINDEX('-%', @domain) > 0
			BEGIN
				SET @isValid = 0
			END

        RETURN @isValid
END
