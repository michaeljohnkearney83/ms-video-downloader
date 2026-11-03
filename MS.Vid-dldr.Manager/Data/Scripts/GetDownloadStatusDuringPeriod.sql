CREATE EXTENSION IF NOT EXISTS tablefunc;
DROP TABLE IF EXISTS StatusCountTemp;
CREATE TEMP TABLE StatusCountTemp(
    requestedon date,
    status text,
    statuscount integer
);

DO $$
DECLARE
    tempRow RECORD;
BEGIN
    FOR tempRow IN
        select newDate::date from generate_series('{0}', '{1}', '1 day'::interval) newDate
    LOOP
        INSERT INTO StatusCountTemp (requestedon, status, statuscount) VALUES (tempRow.newDate, 'NONE', 0);
        INSERT INTO StatusCountTemp (requestedon, status, statuscount) VALUES (tempRow.newDate, 'REQUESTED', 0);
        INSERT INTO StatusCountTemp (requestedon, status, statuscount) VALUES (tempRow.newDate, 'QUEUED', 0);
        INSERT INTO StatusCountTemp (requestedon, status, statuscount) VALUES (tempRow.newDate, 'ANALYSIS', 0);
        INSERT INTO StatusCountTemp (requestedon, status, statuscount) VALUES (tempRow.newDate, 'DOWNLOADING', 0);
        INSERT INTO StatusCountTemp (requestedon, status, statuscount) VALUES (tempRow.newDate, 'FINISHED', 0);
        INSERT INTO StatusCountTemp (requestedon, status, statuscount) VALUES (tempRow.newDate, 'DISTRIBUTED_LOOKUP', 0);
        INSERT INTO StatusCountTemp (requestedon, status, statuscount) VALUES (tempRow.newDate, 'DISTRIBUTED', 0);
        INSERT INTO StatusCountTemp (requestedon, status, statuscount) VALUES (tempRow.newDate, 'DELETED', 0);
        INSERT INTO StatusCountTemp (requestedon, status, statuscount) VALUES (tempRow.newDate, 'ERRORED', 0);
        INSERT INTO StatusCountTemp (requestedon, status, statuscount) VALUES (tempRow.newDate, 'RETRY', 0);
    END LOOP;
END;
$$;

SELECT * FROM crosstab('
    SELECT COALESCE(t2.requestedon, t1.requestedon) as requestedon
        , COALESCE(t2.status, t1.status) as status
        , COALESCE(t2.statuscount, t1.statuscount) as statuscount
    FROM StatusCountTemp AS t1
    LEFT JOIN (SELECT DATE("RequestedOn") AS requestedon,
                          "Status"            AS status,
                          COUNT("Status")     AS statuscount
                   FROM "Downloads"
                   GROUP BY requestedon,
                            Status) AS t2
    ON t1.requestedon = t2.requestedon AND t1.status = t2.status
                  ORDER BY 1, 2
              ', $$VALUES ('NONE'::text), ('REQUESTED'::text), ('QUEUED'::text),
                  ('ANALYSIS'::text), ('DOWNLOADING'::text), ('FINISHED'::text),
                  ('DISTRIBUTED_LOOKUP'::text), ('DISTRIBUTED'::text), ('DELETED'::text),
                  ('ERRORED'::text), ('RETRY'::text)$$
              ) AS statuscount ("requestedon" date,
                  "NONE" int, "REQUESTED" int, "QUEUED" int,
                  "ANALYSIS" int, "DOWNLOADING" int, "FINISHED" int,
                  "DISTRIBUTED_LOOKUP" int, "DISTRIBUTED" int, "DELETED" int,
                  "ERRORED" int, "RETRY" int);