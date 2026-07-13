-- ============================================================================
-- 031 — Update sp_upsert_geo_location to use COALESCE on WHEN MATCHED
-- ============================================================================
-- The original SP blindly overwrote all columns on update. The C# repository
-- used coalesce semantics: only overwrite a column when the new value is
-- non-NULL, otherwise keep the existing value. This update brings the SP in
-- line with that behaviour so the repository can call the SP directly.
-- ----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE [dbo].[sp_upsert_geo_location]
    @computer_id       INT,
    @public_ip_address NVARCHAR(45)  = NULL,
    @gps_latitude      FLOAT         = NULL,
    @gps_longitude     FLOAT         = NULL,
    @gps_last_updated  DATETIME2     = NULL,
    @gps_accuracy      FLOAT         = NULL,
    @ip_latitude       FLOAT         = NULL,
    @ip_longitude      FLOAT         = NULL,
    @ip_last_updated   DATETIME2     = NULL,
    @latitude          FLOAT         = NULL,
    @longitude         FLOAT         = NULL,
    @city              NVARCHAR(100) = NULL,
    @region            NVARCHAR(100) = NULL,
    @country_id        INT           = NULL,
    @isp               NVARCHAR(200) = NULL,
    @timezone          NVARCHAR(50)  = NULL,
    @lookup_successful BIT           = 0,
    @lookup_error      NVARCHAR(500) = NULL,
    @location_source   NVARCHAR(MAX) = N'Auto'
AS
BEGIN
    SET NOCOUNT ON;

    MERGE [dbo].[computer_geo_locations] AS tgt
    USING (SELECT @computer_id AS [computer_id]) AS src
        ON tgt.[computer_id] = src.[computer_id]
    WHEN MATCHED THEN
        UPDATE SET
            [public_ip_address]  = COALESCE(@public_ip_address,  tgt.[public_ip_address]),
            [gps_latitude]       = COALESCE(@gps_latitude,       tgt.[gps_latitude]),
            [gps_longitude]      = COALESCE(@gps_longitude,      tgt.[gps_longitude]),
            [gps_last_updated]   = COALESCE(@gps_last_updated,   tgt.[gps_last_updated]),
            [gps_accuracy_meters]= COALESCE(@gps_accuracy,       tgt.[gps_accuracy_meters]),
            [ip_latitude]        = COALESCE(@ip_latitude,        tgt.[ip_latitude]),
            [ip_longitude]       = COALESCE(@ip_longitude,       tgt.[ip_longitude]),
            [ip_last_updated]    = COALESCE(@ip_last_updated,    tgt.[ip_last_updated]),
            [latitude]           = COALESCE(@latitude,           tgt.[latitude]),
            [longitude]          = COALESCE(@longitude,          tgt.[longitude]),
            [city]               = COALESCE(@city,               tgt.[city]),
            [region]             = COALESCE(@region,             tgt.[region]),
            [country_id]         = COALESCE(@country_id,         tgt.[country_id]),
            [isp]                = COALESCE(@isp,                tgt.[isp]),
            [timezone]           = COALESCE(@timezone,           tgt.[timezone]),
            [last_updated]       = SYSUTCDATETIME(),
            [lookup_successful]  = @lookup_successful,
            [lookup_error]       = @lookup_error,
            [location_source]    = @location_source
    WHEN NOT MATCHED THEN
        INSERT ([computer_id], [public_ip_address],
                [gps_latitude], [gps_longitude], [gps_last_updated], [gps_accuracy_meters],
                [ip_latitude], [ip_longitude], [ip_last_updated],
                [latitude], [longitude], [city], [region], [country_id],
                [isp], [timezone], [last_updated], [lookup_successful],
                [lookup_error], [location_source])
        VALUES (@computer_id, @public_ip_address,
                @gps_latitude, @gps_longitude, @gps_last_updated, @gps_accuracy,
                @ip_latitude, @ip_longitude, @ip_last_updated,
                @latitude, @longitude, @city, @region, @country_id,
                @isp, @timezone, SYSUTCDATETIME(), @lookup_successful,
                @lookup_error, @location_source);
END
GO
