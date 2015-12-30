function dirtyCiphers(userId) {
    var collection = getContext().getCollection();
    var collectionLink = collection.getSelfLink();
    var response = getContext().getResponse();
    var responseBody = {
        updated: 0,
        continuation: true
    };

    if (!userId) throw new Error('The userId is undefined or null.');

    tryQueryAndUpdate();

    function tryQueryAndUpdate(continuation) {
        var query = {
            query: "SELECT * FROM root r WHERE r.UserId = @userId AND r.type = 'cipher' AND r.Dirty = false",
            parameters: [{ name: '@userId', value: userId }]
        };

        var requestOptions = { continuation: continuation };
        var accepted = collection.queryDocuments(collectionLink, query, requestOptions, function (err, retrievedDocs, responseOptions) {
            if (err) throw err;

            if (retrievedDocs.length > 0) {
                tryUpdate(retrievedDocs);
            }
            else if (responseOptions.continuation) {
                tryQueryAndUpdate(responseOptions.continuation);
            }
            else {
                responseBody.continuation = false;
                response.setBody(responseBody);
            }
        });

        if (!accepted) {
            response.setBody(responseBody);
        }
    }

    function tryUpdate(documents) {
        if (documents.length > 0) {
            // dirty it
            documents[0].Dirty = true;

            var accepted = collection.replaceDocument(documents[0]._self, documents[0], {}, function (err, replacedDoc) {
                if (err) throw err;

                responseBody.updated++;
                documents.shift();

                tryUpdate(documents);
            });

            if (!accepted) {
                response.setBody(responseBody);
            }
        }
        else {
            tryQueryAndUpdate();
        }
    }
}
