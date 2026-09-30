-- ═════════════════════════════════════════════════════════════════════════════
--  Técnicos del ERP ficticio.
--
--  El personal de campo del cliente, para asignarlo a las cotizaciones.
--  El Excel de ORKIN no tiene hoja de técnicos (H-11: tampoco clientes), así
--  que estos son ficticios, con nombres y cargos realistas.
-- ═════════════════════════════════════════════════════════════════════════════

USE OrkinClienteDemo;
GO

IF OBJECT_ID('dbo.TECNICO', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TECNICO (
        TEC_CODIGO  nvarchar(20)  NOT NULL CONSTRAINT PK_TECNICO PRIMARY KEY,
        TEC_NOMBRE  nvarchar(200) NOT NULL,
        TEC_TELEFONO nvarchar(30)  NULL,
        TEC_CORREO   nvarchar(200) NULL,
        TEC_CARGO     nvarchar(100) NULL,
        TEC_ACTIVO    bit           NOT NULL CONSTRAINT DF_TEC_ACTIVO DEFAULT (1)
    );
END
GO

IF OBJECT_ID('dbo.vw_TecnicosOrkin', 'V') IS NOT NULL DROP VIEW dbo.vw_TecnicosOrkin;
GO

CREATE VIEW dbo.vw_TecnicosOrkin
AS
SELECT
    TEC_CODIGO  AS Codigo,
    TEC_NOMBRE  AS Nombre,
    TEC_TELEFONO AS Telefono,
    TEC_CORREO  AS Correo,
    TEC_CARGO   AS Cargo,
    ROW_NUMBER() OVER (ORDER BY TEC_CODIGO) AS Orden
FROM dbo.TECNICO
WHERE TEC_ACTIVO = 1;
GO

DELETE FROM dbo.TECNICO;
INSERT INTO dbo.TECNICO (TEC_CODIGO, TEC_NOMBRE, TEC_TELEFONO, TEC_CORREO, TEC_CARGO) VALUES
('TEC-001', 'José Ramírez Silva',     '+505 8888 1001', 'jramirez@demo.ni', 'Técnico de campo'),
('TEC-002', 'María Solís Cornejo',    '+505 8888 1002', 'msolis@demo.ni',   'Supervisora técnica'),
('TEC-003', 'Carlos Mejía Talavera',  '+505 8888 1003', 'cmejia@demo.ni',   'Técnico de campo'),
('TEC-004', 'Ana Ibarra Pinell',      '+505 8888 1004', 'aibarra@demo.ni',  'Técnica de campo'),
('TEC-005', 'Pedro Altamirano Ruiz',  '+505 8888 1005', 'paltamirano@demo.ni','Técnico fumigador'),
('TEC-006', 'Gabriel Ríos Chávez',    '+505 8888 1006', 'grios@demo.ni',    'Técnico de termitas'),
('TEC-007', 'Rosa Chávez Belli',      '+505 8888 1007', 'rchavez@demo.ni',  'Coordinadora de trabajos'),
('TEC-008', 'Oscar Zeledón Torres',   '+505 8888 1008', 'ozeledon@demo.ni', 'Técnico de campo');
GO
