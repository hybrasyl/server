-- Seed the database with a few test maps, warps and items. The content added here
-- should be enough to allow a new user to log in and understand some of the basic
-- functionality of Hybrasyl.

INSERT INTO maps VALUES('500', '100', '100', 'Test Village', 0, NOW(), NOW(), NULL);
INSERT INTO maps VALUES('136', '12', '12', 'Test Inn', '0', NOW(), NOW(), NULL);

-- Add some test items - in particular, sets of clothes so developers don't have
-- to be constantly naked

INSERT INTO items VALUES('66', 'Stick', '86', '-1', '1', '1', '2', '1', '1', '3', '1', 
'1000', '1', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', 
'14', '4', '14', '4', '100', '0', '0', '0', '1', '0', '0', '1', '0', '1', '0', '0', '0', 
'0', '0', NOW(), NOW(), '0', NULL, '0', '0', NULL, NULL, '0', '0', '0', '1', NULL);

INSERT INTO items VALUES('1', 'Shirt', '96', '-1', '1', '1', '2', NULL, '2', '2', '1', 
'2000', '1', '0', '0', '1', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', 
'0', '0', '0', '0', '0', '0', '0', '0', '1', '1', '0', '0', '0', '1', '0', '0', '1', '0', 
'0', NOW(), NOW(), '0', NULL, '0', '0', NULL, NULL, '0', '0', '0', '1', NULL);

INSERT INTO items VALUES('2', 'Blouse', '115', '-1', '1', '1', '2', NULL, '2', '0', 
'1', '0', '0', '0', '0', '2', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0',
'0', '0', '0', '0', '0', '0', '0', '0', '0', '1', '1', '0', '0', '0', '1', '0', '0',
'0', '0', '0', '2013-03-05 04:10:02', '2013-03-15 03:02:52', '0', NULL, '0', '0', 
NULL, NULL, '0', '0', '0', '1', NULL);

--
-- Connect the two test maps using warps
--

INSERT INTO warps VALUES('1', '27', '43', '5', '10', '99', '1', '0', '0', '500', '136', 
NOW(), NOW());
INSERT INTO warps VALUES('2', '32', '37', '10', '2', '99', '1', '0', '0', '500', '136', 
NOW(), NOW());
INSERT INTO warps VALUES('3', '45', '50', '50', '50', '99', '1', '0', '0', '500', '500',
NOW(), NOW());
INSERT INTO warps VALUES('4', '5', '11', '27', '44', '99', '1', '0', '0', '136', '500',
NOW(), NOW());
INSERT INTO warps VALUES('5', '6', '11', '27', '44', '99', '1', '0', '0', '136', '500',
NOW(), NOW());
INSERT INTO warps VALUES('6', '11', '2', '33', '37', '99', '1', '0', '0', '136', '500',
NOW(), NOW());
INSERT INTO warps VALUES('7', '11', '3', '33', '37', '99', '1', '0', '0', '136', '500',
NOW(), NOW());
INSERT INTO warps VALUES('8', '28', '43', '6', '10', '99', '1', '0', '0', '500', '136',
NOW(), NOW());
INSERT INTO warps VALUES('9', '32', '38', '10', '2', '99', '1', '0', '0', '500', '136',
NOW(), NOW());

-- Add a nation named "Mileth", which is required as the default nationality of new Aislings. 
INSERT INTO nations VALUES(NULL, '0', 'Mileth', 'Starting town', NOW(), NOW());

-- Add a spawn point; this is where the user will appear when logging in for the first time.
-- Dynamically pulls the ID of the starting nation since this SQL script may not be imported
-- before anything else is added (can't assume it's ID will be 0).
INSERT INTO spawn_points VALUES(NULL, '136', '5', '5', NOW(), NOW(), (SELECT id FROM nations where name = 'Mileth' LIMIT 1));

-- Add a flag that can be attached to players to make them admins. This flag is recognized by
-- the Hybrasyl binary.
INSERT INTO flags VALUES(NULL, 'gamemaster', 'Admins', NOW(), NOW()); 
