-- ============================================================
-- B2BShop additional schema objects (run after EF Core migration)
-- Views, stored procedures, and triggers
-- ============================================================

-- -----------------------------------------------------------
-- VIEW: company_order_summary
-- Per-company order aggregates — used by dashboard endpoint
-- -----------------------------------------------------------
CREATE OR REPLACE VIEW company_order_summary AS
SELECT
    o."CompanyId",
    COUNT(*)                                  AS total_orders,
    COUNT(*) FILTER (WHERE o."Status" = 1)    AS pending_approval_count,
    COUNT(*) FILTER (WHERE o."Status" = 7)    AS cancelled_count,
    SUM(o."GrandTotal")                       AS total_spend,
    MAX(o."CreatedAt")                        AS last_order_at
FROM "Orders" o
GROUP BY o."CompanyId";

-- -----------------------------------------------------------
-- VIEW: active_price_lists
-- Only currently-valid price lists per company
-- Note: uses server time — TZ-sensitive hidden behavior
-- -----------------------------------------------------------
CREATE OR REPLACE VIEW active_price_lists AS
SELECT pl.*
FROM "PriceLists" pl
WHERE pl."IsActive" = TRUE
  AND pl."ValidFrom" <= NOW()
  AND (pl."ValidTo" IS NULL OR pl."ValidTo" >= NOW());

-- -----------------------------------------------------------
-- VIEW: catalog_availability_v
-- Per-company article availability flattened view
-- Deliberately does NOT apply soft-delete to articles
-- (This inconsistency is intentional — training material)
-- -----------------------------------------------------------
CREATE OR REPLACE VIEW catalog_availability_v AS
SELECT
    a."Id"                     AS article_id,
    a."Code"                   AS article_code,
    a."Name"                   AS article_name,
    a."BasePrice"              AS base_price,
    a."CategoryId"             AS category_id,
    a."IsActive"               AS is_active,
    cpa."CompanyId"            AS company_id,
    cpa."IsVisible"            AS is_visible,
    cpa."IsOrderable"          AS is_orderable,
    cpa."IsExcluded"           AS is_excluded,
    'individual'               AS assignment_source
FROM "Articles" a
JOIN "ProductAssignments" cpa ON cpa."ArticleId" = a."Id"
  AND (cpa."EffectiveFrom" IS NULL OR cpa."EffectiveFrom" <= NOW())
  AND (cpa."EffectiveTo"   IS NULL OR cpa."EffectiveTo"   >= NOW())

UNION ALL

SELECT
    a."Id",
    a."Code",
    a."Name",
    a."BasePrice",
    a."CategoryId",
    a."IsActive",
    cca."CompanyId",
    cca."IsVisible",
    TRUE  AS is_orderable,
    cca."IsExcluded",
    'category' AS assignment_source
FROM "Articles" a
JOIN "CategoryAssignments" cca ON cca."CategoryId" = a."CategoryId"
  AND (cca."EffectiveFrom" IS NULL OR cca."EffectiveFrom" <= NOW())
  AND (cca."EffectiveTo"   IS NULL OR cca."EffectiveTo"   >= NOW())
WHERE NOT EXISTS (
    SELECT 1 FROM "ProductAssignments" pa2
    WHERE pa2."ArticleId" = a."Id" AND pa2."CompanyId" = cca."CompanyId"
);

-- -----------------------------------------------------------
-- STORED PROCEDURE: sp_get_company_pricing_stats
-- Returns pricing statistics for a company — used in admin
-- Bug: counts only active price lists, not all historical ones
-- -----------------------------------------------------------
CREATE OR REPLACE FUNCTION sp_get_company_pricing_stats(p_company_id UUID)
RETURNS TABLE (
    price_list_count    INTEGER,
    contract_count      INTEGER,
    volume_tier_count   INTEGER,
    promo_count         INTEGER,
    pricing_engine      TEXT
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        (SELECT COUNT(*)::INTEGER FROM "PriceLists" pl
         WHERE pl."CompanyId" = p_company_id AND pl."IsActive" = TRUE),
        (SELECT COUNT(*)::INTEGER FROM "ContractPrices" cp
         WHERE cp."CompanyId" = p_company_id AND cp."ValidTo" IS NULL OR cp."ValidTo" >= NOW()),
        (SELECT COUNT(*)::INTEGER FROM "VolumePriceTiers" vt
         JOIN "PriceLists" pl2 ON pl2."Id" = vt."PriceListId"
         WHERE pl2."CompanyId" = p_company_id),
        (SELECT COUNT(*)::INTEGER FROM "PromotionalDiscounts" pd
         WHERE pd."CompanyId" = p_company_id AND pd."IsActive" = TRUE),
        (SELECT c."PricingEngineVersion" FROM "Companies" c WHERE c."Id" = p_company_id LIMIT 1);
END;
$$ LANGUAGE plpgsql;

-- -----------------------------------------------------------
-- TRIGGER: trg_order_audit
-- Appends to AuditLogs whenever an order changes status
-- Intentional: only logs Status column changes
-- -----------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_order_audit_trigger()
RETURNS TRIGGER AS $$
BEGIN
    IF OLD."Status" IS DISTINCT FROM NEW."Status" THEN
        INSERT INTO "AuditLogs" (
            "Id", "EntityType", "EntityId", "Action",
            "OldValuesJson", "NewValuesJson", "OccurredAt"
        ) VALUES (
            gen_random_uuid(),
            'Order',
            NEW."Id",
            'StatusChanged',
            jsonb_build_object('status', OLD."Status")::TEXT,
            jsonb_build_object('status', NEW."Status")::TEXT,
            NOW()
        );
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_order_audit ON "Orders";
CREATE TRIGGER trg_order_audit
    AFTER UPDATE ON "Orders"
    FOR EACH ROW EXECUTE FUNCTION fn_order_audit_trigger();

-- -----------------------------------------------------------
-- TRIGGER: trg_stock_check
-- Prevents ordering when stock reaches zero — but only fires
-- on OrderLines inserts, not updates (deliberate partial guard)
-- -----------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_stock_check_trigger()
RETURNS TRIGGER AS $$
DECLARE
    v_stock INTEGER;
BEGIN
    SELECT COALESCE(SUM(sl."Quantity" - sl."ReservedQuantity"), 0) INTO v_stock
    FROM "StockLevels" sl WHERE sl."ArticleId" = NEW."ArticleId";

    -- Deliberately: only warns via notice, does not block
    IF v_stock < NEW."Quantity" THEN
        RAISE NOTICE 'Article % may have insufficient stock (available: %, requested: %)',
            NEW."ArticleId", v_stock, NEW."Quantity";
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_stock_check ON "OrderLines";
CREATE TRIGGER trg_stock_check
    BEFORE INSERT ON "OrderLines"
    FOR EACH ROW EXECUTE FUNCTION fn_stock_check_trigger();
