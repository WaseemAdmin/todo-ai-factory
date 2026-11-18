import { TestBed } from '@angular/core/testing';
import { TodoApiService } from '../../core/api/todo-api.service';
import { Todo, TodoUpdate, TodoWrite } from '../../core/api/todo.model';
import { TodoPage } from './todo-page';

const initial: Todo = {
  id: 'one', title: 'First task', description: null, priority: 'Normal', dueAtUtc: null,
  isCompleted: false, completedAtUtc: null, isCancelled: false, cancelledAtUtc: null,
  createdAtUtc: '2026-11-18T10:00:00Z', updatedAtUtc: '2026-11-18T10:00:00Z'
};

class FakeApi {
  rows: Todo[] = [{ ...initial }];
  fail = false;
  async list(isCompleted?: boolean, isCancelled?: boolean): Promise<Todo[]> {
    if (this.fail) throw new Error('offline');
    return this.rows.filter(row => {
      return row.isCancelled === (isCancelled === true) && (isCompleted === undefined || row.isCompleted === isCompleted);
    });
  }
  async get(id: string): Promise<Todo> { return { ...this.rows.find(row => row.id === id)! }; }
  async create(request: TodoWrite): Promise<Todo> {
    const row = { ...initial, ...request, id: 'new' };
    this.rows.push(row);
    return row;
  }
  async update(id: string, request: TodoUpdate): Promise<Todo> {
    const row = this.rows.find(row => row.id === id)!;
    Object.assign(row, request);
    row.completedAtUtc = request.isCompleted ? '2026-11-18T11:00:00Z' : null;
    return row;
  }
  async delete(id: string): Promise<void> {
    const row = this.rows.find(value => value.id === id)!;
    row.isCancelled = true;
    row.cancelledAtUtc = '2026-11-18T12:00:00Z';
  }
}

describe('TodoPage', () => {
  let api: FakeApi;
  let page: TodoPage;
  let fixture: ReturnType<typeof TestBed.createComponent<TodoPage>>;

  beforeEach(async () => {
    api = new FakeApi();
    await TestBed.configureTestingModule({ imports: [TodoPage], providers: [{ provide: TodoApiService, useValue: api }] }).compileComponents();
    fixture = TestBed.createComponent(TodoPage);
    page = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('renders the loaded list', () => {
    expect(fixture.nativeElement.textContent).toContain('First task');
    expect(page.todos()).toHaveLength(1);
  });

  it('loads details for a selected todo', async () => {
    await page.view(page.todos()[0]);
    expect(page.detail()?.id).toBe('one');
    await page.view(page.todos()[0]);
    expect(page.detail()).toBeNull();
  });

  it('shows a loading state while a list request is pending', async () => {
    let finish!: (rows: Todo[]) => void;
    vi.spyOn(api, 'list').mockImplementationOnce(() => new Promise(resolve => { finish = resolve; }));
    const pending = page.reload();
    fixture.detectChanges();
    expect(page.loading()).toBe(true);
    expect(fixture.nativeElement.querySelector('[role="status"]')?.textContent).toContain('Loading todos');
    finish([{ ...initial }]);
    await pending;
    expect(page.loading()).toBe(false);
  });

  it('creates and edits a todo', async () => {
    page.add();
    expect(page.editorOpen()).toBe(true);
    await page.save({ title: 'New task', description: null, priority: 'High', dueAtUtc: null });
    expect(page.editorOpen()).toBe(false);
    expect(page.todos().map(row => row.title)).toContain('New task');
    page.edit(page.todos().find(row => row.id === 'new')!);
    await page.save({ title: 'Renamed task', description: null, priority: 'Low', dueAtUtc: null });
    expect(page.todos().map(row => row.title)).toContain('Renamed task');
  });

  it('completes, filters, and reopens a todo', async () => {
    await page.toggle(page.todos()[0]);
    expect(page.todos()[0].isCompleted).toBe(true);
    page.setFilter('completed');
    await page.reload();
    expect(page.todos()).toHaveLength(1);
    await page.toggle(page.todos()[0]);
    expect(page.todos()).toHaveLength(0);
    page.setFilter('active');
    await page.reload();
    expect(page.todos()[0].isCompleted).toBe(false);
  });

  it('cancels only after confirmation and leaves the row available in the Cancelled view', async () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    const cancelAction = () => Array.from(fixture.nativeElement.querySelectorAll('app-todo-card button') as NodeListOf<HTMLButtonElement>)
      .find(button => button.textContent?.trim() === 'Cancel');
    fixture.detectChanges();
    expect(cancelAction()).toBeDefined();
    expect(fixture.nativeElement.textContent).not.toContain('Delete');
    cancelAction()!.click();
    await fixture.whenStable();
    expect(page.todos()).toHaveLength(1);
    confirm.mockReturnValue(true);
    cancelAction()!.click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(page.todos()).toHaveLength(0);
    expect((api.rows[0] as Todo & { isCancelled: boolean }).isCancelled).toBe(true);
    const cancelledButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find((button: HTMLButtonElement) => button.textContent?.trim() === 'Cancelled') as HTMLButtonElement | undefined;
    expect(cancelledButton).toBeDefined();
    cancelledButton!.click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('First task');
    expect(fixture.nativeElement.textContent).toContain('Cancelled');
    const card = fixture.nativeElement.querySelector('app-todo-card') as HTMLElement;
    expect(card.textContent).toContain('2026');
    const actions = Array.from(card.querySelectorAll('button')).map(button => button.textContent?.trim());
    expect(actions).not.toContain('Edit');
    expect(actions).not.toContain('Cancel');
    expect((card.querySelector('button[aria-label^="Complete"]') as HTMLButtonElement).disabled).toBe(true);
    confirm.mockRestore();
  });

  it('shows API errors and clears the loading state', async () => {
    api.fail = true;
    await page.reload();
    fixture.detectChanges();
    expect(page.loading()).toBe(false);
    expect(fixture.nativeElement.querySelector('[role="alert"]')).not.toBeNull();
  });
});
