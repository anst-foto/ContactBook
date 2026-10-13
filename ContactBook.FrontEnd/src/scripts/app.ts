import { ApiError, Contact, ContactType, ContactsApi } from "./api.js";

const api = new ContactsApi();

function el<T extends HTMLElement>(id: string): T {
    const node = document.getElementById(id);
    if (!node) throw new Error(`Element #${id} not found`);
    return node as T;
}

const createForm = el<HTMLFormElement>("create-form");
const createMessage = el<HTMLDivElement>("create-message");
const searchForm = el<HTMLFormElement>("search-form");
const searchInput = el<HTMLInputElement>("search-input");
const searchClear = el<HTMLButtonElement>("search-clear");
const listLoading = el<HTMLDivElement>("list-loading");
const listEmpty = el<HTMLDivElement>("list-empty");
const contactsList = el<HTMLUListElement>("contacts-list");

const editDialog = el<HTMLDialogElement>("edit-dialog");
const editForm = el<HTMLFormElement>("edit-form");
const editMessage = el<HTMLDivElement>("edit-message");
const editCancel = el<HTMLButtonElement>("edit-cancel");

function setMessage(node: HTMLElement, text: string, kind: "error" | "success" | "info" = "info"): void {
    node.textContent = text;
    node.className = `message message-${kind}`;
}

function clearMessage(node: HTMLElement): void {
    node.textContent = "";
    node.className = "message";
}

function showValidationErrors(node: HTMLElement, err: ApiError): void {
    if (err.validationErrors) {
        const lines = Object.entries(err.validationErrors)
            .flatMap(([field, messages]) => messages.map(m => `${field}: ${m}`));
        setMessage(node, lines.join("\n"), "error");
    } else {
        setMessage(node, err.message, "error");
    }
}

function renderContact(c: Contact): HTMLLIElement {
    const li = document.createElement("li");
    li.className = "contact-item";

    const info = document.createElement("div");
    info.className = "contact-info";

    const name = document.createElement("div");
    name.className = "contact-name";
    name.textContent = c.name;

    const value = document.createElement("div");
    value.className = "contact-value";
    value.textContent = c.contact;

    const meta = document.createElement("div");
    meta.className = "contact-meta";

    const typeBadge = document.createElement("span");
    typeBadge.className = `badge badge-${c.type.toLowerCase()}`;
    typeBadge.textContent = c.type === "Email" ? "Email" : "Телефон";

    const shortIdBadge = document.createElement("span");
    shortIdBadge.className = "badge badge-shortid";
    shortIdBadge.textContent = `#${c.shortId}`;
    shortIdBadge.title = c.id;

    meta.append(typeBadge, shortIdBadge);
    info.append(name, value, meta);

    const actions = document.createElement("div");
    actions.className = "contact-actions";

    const editBtn = document.createElement("button");
    editBtn.type = "button";
    editBtn.className = "btn btn-ghost btn-sm";
    editBtn.textContent = "Изменить";
    editBtn.addEventListener("click", () => openEditDialog(c));

    const deleteBtn = document.createElement("button");
    deleteBtn.type = "button";
    deleteBtn.className = "btn btn-danger btn-sm";
    deleteBtn.textContent = "Удалить";
    deleteBtn.addEventListener("click", () => void onDelete(c));

    actions.append(editBtn, deleteBtn);
    li.append(info, actions);
    return li;
}

function showLoading(): void {
    listLoading.classList.remove("hidden");
    listEmpty.classList.add("hidden");
    contactsList.classList.add("hidden");
}

function renderList(contacts: Contact[]): void {
    listLoading.classList.add("hidden");

    if (contacts.length === 0) {
        listEmpty.classList.remove("hidden");
        contactsList.classList.add("hidden");
        contactsList.replaceChildren();
        return;
    }

    listEmpty.classList.add("hidden");
    contactsList.classList.remove("hidden");

    const items = contacts.map(renderContact);
    contactsList.replaceChildren(...items);
}

async function loadContacts(search?: string): Promise<void> {
    showLoading();
    try {
        const contacts = await api.list(search);
        renderList(contacts);
    } catch (err) {
        listLoading.classList.add("hidden");
        if (err instanceof ApiError) {
            setMessage(listEmpty, err.message, "error");
            listEmpty.classList.remove("hidden");
        } else {
            setMessage(listEmpty, "Не удалось загрузить контакты", "error");
            listEmpty.classList.remove("hidden");
        }
    }
}

function openEditDialog(c: Contact): void {
    clearMessage(editMessage);
    const form = editForm.elements;
    (form.namedItem("id") as HTMLInputElement).value = c.id;
    (form.namedItem("name") as HTMLInputElement).value = c.name;
    (form.namedItem("contact") as HTMLInputElement).value = c.contact;
    (form.namedItem("type") as HTMLSelectElement).value = c.type;
    editDialog.showModal();
}

async function onDelete(c: Contact): Promise<void> {
    if (!window.confirm(`Удалить контакт «${c.name}»?`)) return;
    try {
        await api.delete(c.id);
        await loadContacts(searchInput.value);
    } catch (err) {
        const message = err instanceof ApiError ? err.message : "Не удалось удалить контакт";
        window.alert(message);
    }
}

createForm.addEventListener("submit", async e => {
    e.preventDefault();
    clearMessage(createMessage);

    const form = createForm.elements;
    const name = (form.namedItem("name") as HTMLInputElement).value.trim();
    const contact = (form.namedItem("contact") as HTMLInputElement).value.trim();
    const type = (form.namedItem("type") as HTMLSelectElement).value as ContactType;

    if (name.length === 0 || contact.length === 0) {
        setMessage(createMessage, "Заполните имя и контакт", "error");
        return;
    }

    try {
        await api.create({ name, contact, type });
        createForm.reset();
        setMessage(createMessage, `Контакт «${name}» создан`, "success");
        await loadContacts(searchInput.value);
    } catch (err) {
        if (err instanceof ApiError) showValidationErrors(createMessage, err);
        else setMessage(createMessage, "Не удалось создать контакт", "error");
    }
});

searchForm.addEventListener("submit", e => {
    e.preventDefault();
    void loadContacts(searchInput.value);
});

searchClear.addEventListener("click", () => {
    searchInput.value = "";
    void loadContacts();
});

editCancel.addEventListener("click", () => editDialog.close());

editForm.addEventListener("submit", async e => {
    e.preventDefault();
    clearMessage(editMessage);

    const form = editForm.elements;
    const id = (form.namedItem("id") as HTMLInputElement).value;
    const name = (form.namedItem("name") as HTMLInputElement).value.trim();
    const contact = (form.namedItem("contact") as HTMLInputElement).value.trim();
    const type = (form.namedItem("type") as HTMLSelectElement).value as ContactType;

    if (name.length === 0 || contact.length === 0) {
        setMessage(editMessage, "Заполните имя и контакт", "error");
        return;
    }

    try {
        await api.update({ id, name, contact, type });
        editDialog.close();
        await loadContacts(searchInput.value);
    } catch (err) {
        if (err instanceof ApiError) showValidationErrors(editMessage, err);
        else setMessage(editMessage, "Не удалось сохранить изменения", "error");
    }
});

void loadContacts();