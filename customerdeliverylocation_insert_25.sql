-- customerdeliverylocation に 25 行追加（customerid はすべて 1）
-- deliverylocationid はシーケンスで自動採番されるため指定しない
--
-- 既存データがある場合、シーケンスを現在の最大IDに合わせる（主キー重複を防ぐ）
SELECT setval(
    'customerdeliverylocation_deliverylocationid_seq',
    (SELECT COALESCE(MAX(deliverylocationid), 0) FROM customerdeliverylocation)
);

INSERT INTO customerdeliverylocation (
    customerid,
    locationcode,
    locationname,
    locationshortname,
    postalcode,
    address1,
    address2,
    phonenumber,
    faxnumber,
    contactpersonname,
    contactpersonemail,
    deliverynote,
    isdefault,
    isactive,
    sortorder,
    numberofdeliverynotes
) VALUES
    (1, 'LOC001', '納入場所1', '場所1', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, true, true, 1, NULL),
    (1, 'LOC002', '納入場所2', '場所2', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, false, true, 2, NULL),
    (1, 'LOC003', '納入場所3', '場所3', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, false, true, 3, NULL),
    (1, 'LOC004', '納入場所4', '場所4', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, false, true, 4, NULL),
    (1, 'LOC005', '納入場所5', '場所5', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, false, true, 5, NULL),
    (1, 'LOC006', '納入場所6', '場所6', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, false, true, 6, NULL),
    (1, 'LOC007', '納入場所7', '場所7', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, false, true, 7, NULL),
    (1, 'LOC008', '納入場所8', '場所8', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, false, true, 8, NULL),
    (1, 'LOC009', '納入場所9', '場所9', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, false, true, 9, NULL),
    (1, 'LOC010', '納入場所10', '場所10', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, false, true, 10, NULL),
    (1, 'LOC011', '納入場所11', '場所11', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, false, true, 11, NULL),
    (1, 'LOC012', '納入場所12', '場所12', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, false, true, 12, NULL),
    (1, 'LOC013', '納入場所13', '場所13', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, false, true, 13, NULL),
    (1, 'LOC014', '納入場所14', '場所14', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, false, true, 14, NULL),
    (1, 'LOC015', '納入場所15', '場所15', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, false, true, 15, NULL),
    (1, 'LOC016', '納入場所16', '場所16', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, false, true, 16, NULL),
    (1, 'LOC017', '納入場所17', '場所17', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, false, true, 17, NULL),
    (1, 'LOC018', '納入場所18', '場所18', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, false, true, 18, NULL),
    (1, 'LOC019', '納入場所19', '場所19', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, false, true, 19, NULL),
    (1, 'LOC020', '納入場所20', '場所20', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, false, true, 20, NULL),
    (1, 'LOC021', '納入場所21', '場所21', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, false, true, 21, NULL),
    (1, 'LOC022', '納入場所22', '場所22', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, false, true, 22, NULL),
    (1, 'LOC023', '納入場所23', '場所23', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, false, true, 23, NULL),
    (1, 'LOC024', '納入場所24', '場所24', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, false, true, 24, NULL),
    (1, 'LOC025', '納入場所25', '場所25', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, false, true, 25, NULL);
