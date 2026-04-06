-- Добавление ClassId в таблицу Tests
ALTER TABLE "Tests" ADD COLUMN IF NOT EXISTS "ClassId" uuid NULL;

-- Создание индекса
CREATE INDEX IF NOT EXISTS "IX_Tests_ClassId" ON "Tests" ("ClassId");

-- Создание внешнего ключа
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint 
        WHERE conname = 'FK_Tests_Classes_ClassId'
    ) THEN
        ALTER TABLE "Tests" 
        ADD CONSTRAINT "FK_Tests_Classes_ClassId" 
        FOREIGN KEY ("ClassId") REFERENCES "Classes" ("Id") 
        ON DELETE SET NULL;
    END IF;
END $$;
