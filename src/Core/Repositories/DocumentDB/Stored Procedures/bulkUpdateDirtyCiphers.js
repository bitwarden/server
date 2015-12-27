// Update an array of dirty ciphers for a user.

function bulkUpdateDirtyCiphers(ciphers, userId) {
    var context = getContext();
    var collection = context.getCollection();
    var collectionLink = collection.getSelfLink();
    var response = context.getResponse();

    var count = 0;

    // Validate input.
    if (!ciphers) {
        throw new Error('The ciphers array is undefined or null.');
    }

    var ciphersLength = ciphers.length;
    if (ciphersLength == 0) {
        response.setBody(0);
        return;
    }

    queryAndReplace(ciphers[count]);

    function queryAndReplace(cipher, continuation) {
        var query = {
            query: "SELECT * FROM root r WHERE r.id = @id AND r.UserId = @userId AND r.type = 'cipher' AND r.Dirty = true",
            parameters: [{ name: '@id', value: cipher.id }, { name: '@userId', value: userId }]
        };

        var requestOptions = { continuation: continuation };
        var accepted = collection.queryDocuments(collectionLink, query, requestOptions, function (err, documents, responseOptions) {
            if (err) throw err;

            if (documents.length > 0) {
                replace(documents[0], cipher);
            }
            else if (responseOptions.continuation) {
                // try again
                queryAndReplace(cipher, responseOptions.continuation);
            }
            else {
                // doc not found, skip it
                next();
            }
        });

        if (!accepted) {
            response.setBody(count);
        }
    }

    function replace(doc, placementCipher) {
        // site
        if (doc.CipherType == 1) {
            doc.Username = placementCipher.Username;
            doc.Password = placementCipher.Password;
            doc.Notes = placementCipher.Notes;
            doc.Uri = placementCipher.Uri;
        }

        doc.Name = placementCipher.Name;
        doc.RevisionDate = placementCipher.RevisionDate;
        // no longer dirty
        doc.Dirty = false;

        var accepted = collection.replaceDocument(doc._self, doc, function (err) {
            if (err) throw err;

            next();
        });

        if (!accepted) {
            response.setBody(count);
        }
    }

    function next() {
        count++;

        if (count >= ciphersLength) {
            response.setBody(count);
        }
        else {
            queryAndReplace(ciphers[count]);
        }
    }
}
