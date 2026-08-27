-- Cambio aditivo, no destructivo: añade el flag AllowsAiGeneration a Categories
-- y excluye la categoría iECS (propia de Grupo Pronet) de la generación por IA,
-- sin borrar ningún dato.
SET QUOTED_IDENTIFIER ON;
GO

USE TechEvalDb;
GO

IF COL_LENGTH('dbo.Categories','AllowsAiGeneration') IS NULL
    ALTER TABLE dbo.Categories ADD AllowsAiGeneration BIT NOT NULL
        CONSTRAINT DF_Categories_AllowsAiGeneration DEFAULT 1;
GO

UPDATE dbo.Categories SET AllowsAiGeneration = 0 WHERE Name = 'iECS';
GO

PRINT 'Esquema actualizado: AllowsAiGeneration añadido, iECS excluida de generación por IA.';
GO
